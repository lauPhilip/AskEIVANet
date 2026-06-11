import requests
import os
import time
from pathlib import Path
from dotenv import load_dotenv

load_dotenv()

class FreshdeskCrawler:
    """The unified extraction array for EIVA's secure Freshdesk Database and Knowledge Base."""
    
    def __init__(self, domain: str):
        self.domain = domain
        self.base_url = f"https://{domain}.freshdesk.com/api/v2"
        
        self.api_key = os.getenv("FRESHDESK_API_KEY")
        if not self.api_key or self.api_key == "we_will_add_this_later_when_we_get_internal_access":
            raise ValueError("API Key missing or invalid. Check your .env file.")
            
        self.auth = (self.api_key, "X")
        self.download_dir = Path("data/raw_docs")
        self.download_dir.mkdir(parents=True, exist_ok=True)

    def _get(self, endpoint: str, params: dict = None):
        """Modified to handle both list and search object responses."""
        url = f"{self.base_url}/{endpoint}"
        try:
            response = requests.get(url, auth=self.auth, params=params)
            response.raise_for_status()
            time.sleep(0.4) # Slightly increased for the Search API's tighter limits
            return response.json()
        except requests.exceptions.RequestException as e:
            print(f"Connection failed on {endpoint}: {e}")
            return None # Return None so we can distinguish between empty list and error

    def fetch_tickets(self, page: int = 1, updated_since: str = None) -> list:
        """
        Fetches tickets using the standard list API with a date filter.
        The standard API allows up to 40 pages (4,000 tickets) per filter.
        """
        params = {
            "page": page,
            "per_page": 100, # Back to 100 per page for efficiency
            "include": "description",
            "order_by": "updated_at",
            "order_type": "asc"
        }
        
        if updated_since:
            params["updated_since"] = updated_since

        # We switch back to the standard /tickets endpoint but with 
        # a 'since' date to find the historical ones.
        return self._get("tickets", params=params)

    def fetch_ticket_conversations(self, ticket_id: int) -> list:
        """Fetches the entire thread of replies and notes for a specific ticket."""
        return self._get(f"tickets/{ticket_id}/conversations")

    def download_knowledge_base(self):
        """Traverses Categories -> Folders -> Articles to download attached PDFs."""
        print("Initiating deep-dive into the Knowledge Base...")
        
        categories = self._get("solutions/categories")
        if not categories:
            print("No categories found or access denied.")
            return

        for category in categories:
            cat_id = category['id']
            cat_name = category['name']
            print(f"\nScanning Category: [{cat_name}]")
            
            folders = self._get(f"solutions/categories/{cat_id}/folders")
            for folder in folders:
                folder_id = folder['id']
                folder_name = folder['name']
                print(f"  -> Accessing Folder: {folder_name}")
                
                articles = self._get(f"solutions/folders/{folder_id}/articles")
                for article in articles:
                    attachments = article.get('attachments', [])
                    
                    for attachment in attachments:
                        if attachment['name'].lower().endswith('.pdf'):
                            self._download_pdf(attachment['attachment_url'], attachment['name'], folder_name)

    def _download_pdf(self, url: str, filename: str, folder_name: str):
        """Streams the PDF file securely to the local disk without conflicting auth headers."""
        safe_folder = "".join(c for c in folder_name if c.isalnum() or c in (' ', '-', '_')).rstrip()
        target_dir = self.download_dir / safe_folder
        target_dir.mkdir(exist_ok=True)
        
        file_path = target_dir / filename
        
        if file_path.exists():
            return
            
        print(f"     [Downloading]: {filename}")
        try:
            response = requests.get(url, stream=True) 
            response.raise_for_status()
            
            with open(file_path, 'wb') as f:
                for chunk in response.iter_content(chunk_size=8192):
                    f.write(chunk)
            time.sleep(0.5) 
        except Exception as e:
            print(f"     [Failed to download] {filename}: {e}")

if __name__ == "__main__":
    crawler = FreshdeskCrawler(domain="eiva")
    
    print("--- Initiating Full Documentation Retrieval ---")
    # This is the command that actually scans categories and downloads the PDFs
    crawler.download_knowledge_base()
    
    print("\n--- Retrieval Sequence Complete ---")
    print("Check your 'data/raw_docs' folder. You should see the cargo arriving.")