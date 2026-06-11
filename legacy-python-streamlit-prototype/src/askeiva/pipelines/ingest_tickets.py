import os
import weaviate
from weaviate.util import generate_uuid5
from dotenv import load_dotenv
import time

# Relative imports for the pipeline neighborhood
from .freshdesk_crawler import FreshdeskCrawler
from .ticket_processor import TicketProcessor

load_dotenv()

class DataIngestionEngine:
    """Orchestrates the ingestion of structured tickets into Weaviate as semantic chunks."""
    
    def __init__(self):
        # Establish link to the cloud cluster
        self.client = weaviate.connect_to_weaviate_cloud(
            cluster_url=os.getenv("WEAVIATE_URL"),
            auth_credentials=weaviate.auth.AuthApiKey(os.getenv("WEAVIATE_API_KEY")),
            headers={
                "X-Mistral-Api-Key": os.getenv("MISTRAL_API_KEY")
            }
        )
        self.collection = self.client.collections.get("KnowledgeNode")

    def run_ticket_ingestion(self, domain="eiva", start_page=1, max_pages=300):
        """
        A persistent loop that marches through the archives until no more tickets are found.
        """
        print(f"--- Initiating Continuous Archive Retrieval for {domain} ---")
        crawler = FreshdeskCrawler(domain=domain)
        processor = TicketProcessor()
        
        page_num = start_page
        consecutive_empty_pages = 0

        while page_num < (start_page + max_pages):
            print(f"\n[🔄] Processing Page {page_num}...")
            
            # 1. Fetch from Search API (30 results per page)
            raw_tickets = crawler.fetch_tickets(page=page_num)
            
            if not raw_tickets:
                # If we get an empty page, check one more just in case of a fluke
                consecutive_empty_pages += 1
                if consecutive_empty_pages >= 2:
                    print(f"🏁 End of history reached at page {page_num}. Closing loop.")
                    break
                page_num += 1
                continue
            
            consecutive_empty_pages = 0 # Reset if we found data

            # 2. Deep-dive into threads (This takes time, naturally creating a delay)
            clean_tickets = processor.process_tickets_with_dialogue(raw_tickets, crawler, domain)

            # 3. Batch Upload to Weaviate
            with self.collection.batch.dynamic() as batch:
                for ticket in clean_tickets:
                    chunks = processor.chunk_text(ticket["content"])
                    
                    for i, chunk_content in enumerate(chunks):
                        chunk_id = f"{ticket['source_id']}_p{page_num}_part_{i}"
                        batch.add_object(
                            uuid=generate_uuid5(chunk_id),
                            properties={
                                "source_id": chunk_id,
                                "data_type": ticket["data_type"],
                                "subject": ticket['subject'],
                                "content": chunk_content,
                                "is_distilled": False, # Important for your Graph logic
                                "url": ticket["url"]
                            }
                        )
            
            print(f"✅ Page {page_num} stored. (approx {len(raw_tickets)} tickets)")
            
            # 4. The "Cool Down" - Prevents 503 errors and API bans
            # This allows the background workers at Mistral and Weaviate to breathe
            print("⏳ Cooling down for 20 seconds...")
            time.sleep(20) 
            
            page_num += 1

        print("\n--- [ FULL INGESTION SEQUENCE COMPLETE ] ---")

    def close(self):
        self.client.close()

if __name__ == "__main__":
    engine = DataIngestionEngine()
    try:
        # start_page=1 to go from the beginning
        # max_pages=300 to cover up to 9,000 tickets (30 per page)
        engine.run_ticket_ingestion(domain="eiva", start_page=1, max_pages=300)
    finally:
        engine.close()