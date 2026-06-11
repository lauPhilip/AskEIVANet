import os
import weaviate
from weaviate.util import generate_uuid5
from dotenv import load_dotenv
from pathlib import Path
from tqdm import tqdm

# Relative imports from the pipelines directory
from .freshdesk_crawler import FreshdeskCrawler
from .pdf_processor import PDFProcessor
from .ticket_processor import TicketProcessor

load_dotenv()

class KBIngestionEngine:
    def __init__(self):
        # Initialize Weaviate Cloud client
        self.client = weaviate.connect_to_weaviate_cloud(
            cluster_url=os.getenv("WEAVIATE_URL"),
            auth_credentials=weaviate.auth.AuthApiKey(os.getenv("WEAVIATE_API_KEY")),
            headers={"X-Mistral-Api-Key": os.getenv("MISTRAL_API_KEY")}
        )
        self.doc_collection = self.client.collections.get("DocumentLibrary")
        
        # Initialize specialized processing tools
        self.crawler = FreshdeskCrawler(domain="eiva")
        self.pdf_engine = PDFProcessor()
        self.html_cleaner = TicketProcessor()

    def extract_technical_entities(self, text):
        """Uses Mistral to identify the core technical entities for the Knowledge Graph."""
        prompt = f"""
        Extract EIVA technical entities from the following text. 
        Focus on: Product Name (e.g. NaviPac, NaviScan, ScanFish), Software Version, and Key Components.
        
        Format strictly as: [Product] | [Version] | [Keywords]
        TEXT: {text[:1500]}
        """
        try:
            # Reusing the Mistral client from the pdf_engine
            response = self.pdf_engine.client.chat.complete(
                model="mistral-large-latest",
                messages=[{"role": "user", "content": prompt}]
            )
            return response.choices[0].message.content.strip()
        except Exception:
            return "General | N/A | EIVA Technical"

    def run_assimilation(self):
        print("--- Initiating Intelligent KB Assimilation ---")
        categories = self.crawler._get("solutions/categories")
        
        if not categories:
            print("[!] No categories found on Freshdesk.")
            return

        for category in categories:
            folders = self.crawler._get(f"solutions/categories/{category['id']}/folders")
            for folder in folders:
                articles = self.crawler._get(f"solutions/folders/{folder['id']}/articles")
                
                desc = f"Processing: {folder['name'][:25]}"
                with self.doc_collection.batch.dynamic() as batch:
                    for article in tqdm(articles, desc=desc, unit="art"):
                        self._process_article(article, folder['name'], batch)

    def _process_article(self, article, folder_name, batch):
        title = article['title']
        raw_html = article.get('description', '')
        base_text = self.html_cleaner.clean_html(raw_html)
        
        all_image_paths = []
        full_content = f"Source: EIVA Knowledge Base\nFolder: {folder_name}\nTitle: {title}\n\n{base_text}"
        
        # 1. Handle PDF Attachments (OCR + Local Image Extraction)
        attachments = article.get('attachments', [])
        for att in attachments:
            if att['name'].lower().endswith('.pdf'):
                self.crawler._download_pdf(att['attachment_url'], att['name'], folder_name)
                # Clean folder name for path compatibility
                safe_folder = "".join(c for c in folder_name if c.isalnum() or c in (' ', '-', '_')).rstrip()
                local_path = self.crawler.download_dir / safe_folder / att['name']
                
                result = self.pdf_engine.process_document(local_path)
                if result["markdown"]:
                    full_content += f"\n\n--- ATTACHMENT: {att['name']} ---\n{result['markdown']}"
                all_image_paths.extend(result["local_image_paths"])

        # 2. Semantic Chunking
        # Conservative limit to stay safely under Mistral's 8192 token wall
        chunks = self.html_cleaner.chunk_text(full_content, max_chars=8000)
        
        # 3. Intelligent Tagging and Ingestion
        for i, chunk_content in enumerate(chunks):
            # Each chunk gets an LLM-powered entity extraction for the Knowledge Graph
            tags = self.extract_technical_entities(chunk_content)
            
            chunk_id = f"kb_{article['id']}_part_{i}"
            deterministic_uuid = generate_uuid5(chunk_id)
            
            batch.add_object(
                uuid=deterministic_uuid,
                properties={
                    "document_id": chunk_id,
                    "title": f"{title} (Part {i+1})" if len(chunks) > 1 else title,
                    "document_type": "KB_Article",
                    "content": chunk_content,
                    "tags": tags,
                    "url": f"https://eiva.freshdesk.com/a/solutions/articles/{article['id']}",
                    "image_urls": all_image_paths 
                }
            )

    def close(self):
        self.client.close()

if __name__ == "__main__":
    engine = KBIngestionEngine()
    try:
        engine.run_assimilation()
    finally:
        engine.close()