import os
import fitz  # PyMuPDF
from pathlib import Path
from mistralai.client import Mistral
from dotenv import load_dotenv

load_dotenv()

class PDFProcessor:
    """The multimodal engine. Extracts raw images locally and uses Mistral OCR for text/tables."""
    
    def __init__(self):
        self.api_key = os.getenv("MISTRAL_API_KEY")
        if not self.api_key:
            raise ValueError("Mistral API key is missing.")
        self.client = Mistral(api_key=self.api_key)

    def _extract_images_locally(self, file_path: Path) -> list:
        """Physically extracts images from PDF and saves them to a local directory."""
        img_dir = file_path.parent / "images" / file_path.stem
        img_dir.mkdir(parents=True, exist_ok=True)
        
        extracted_paths = []
        doc = fitz.open(file_path)
        
        for page_index in range(len(doc)):
            page = doc[page_index]
            image_list = page.get_images(full=True)
            
            for img_index, img in enumerate(image_list):
                xref = img[0]
                base_image = doc.extract_image(xref)
                image_bytes = base_image["image"]
                ext = base_image["ext"]
                
                img_name = f"page{page_index+1}_img{img_index+1}.{ext}"
                final_path = img_dir / img_name
                
                with open(final_path, "wb") as f:
                    f.write(image_bytes)
                
                extracted_paths.append(str(final_path))
                
        doc.close()
        return extracted_paths

    def process_document(self, file_path: Path) -> dict:
        """Performs a multimodal scan: local image extraction + Mistral OCR."""
        if not file_path.exists():
            return {"markdown": "", "local_image_paths": []}
            
        local_images = self._extract_images_locally(file_path)
        
        try:
            with open(file_path, "rb") as f:
                uploaded_file = self.client.files.upload(
                    file={"file_name": file_path.name, "content": f.read()},
                    purpose="ocr"
                )
            
            ocr_response = self.client.ocr.process(
                model="mistral-ocr-latest",
                document={
                    "type": "document_url",
                    "document_url": self.client.files.get_signed_url(file_id=uploaded_file.id).url,
                }
            )
            
            extracted_markdown = ""
            for page in ocr_response.pages:
                extracted_markdown += f"\n\n{page.markdown}"
                
            self.client.files.delete(file_id=uploaded_file.id)
            
            return {
                "markdown": extracted_markdown,
                "local_image_paths": local_images
            }

        except Exception as e:
            return {"markdown": "", "local_image_paths": local_images}