import os
import weaviate
from weaviate.classes.config import Configure, Property, DataType, ReferenceProperty
from dotenv import load_dotenv

load_dotenv()

class DatabaseManager:
    """Manages the connection and schema for the Weaviate Cloud cluster using v4 standards."""
    
    def __init__(self):
        self.client = weaviate.connect_to_weaviate_cloud(
            cluster_url=os.getenv("WEAVIATE_URL"),
            auth_credentials=weaviate.auth.AuthApiKey(os.getenv("WEAVIATE_API_KEY")),
            headers={
                "X-Mistral-Api-Key": os.getenv("MISTRAL_API_KEY")
            }
        )

    def initialize_schema(self):
        """Builds the triple-collection schema using the modern vector_config syntax."""
        if not self.client.is_ready():
            print("Failed to connect to the cloud cluster.")
            return

        # 1. Forge the Document Library (The Manuals)
        doc_name = "DocumentLibrary"
        if not self.client.collections.exists(doc_name):
            print(f"Constructing '{doc_name}' with modern vector config...")
            self.client.collections.create(
                name=doc_name,
                vector_config=Configure.VectorIndex.hnsw(),
                properties=[
                    Property(name="document_id", data_type=DataType.TEXT, skip_vectorization=True),
                    Property(name="title", data_type=DataType.TEXT),
                    Property(name="document_type", data_type=DataType.TEXT),
                    Property(name="content", data_type=DataType.TEXT),
                    Property(name="url", data_type=DataType.TEXT, skip_vectorization=True),
                    Property(name="image_urls", data_type=DataType.TEXT_ARRAY, skip_vectorization=True),
                    Property(name="tags", data_type=DataType.TEXT, description="Mistral-extracted entities")
                ]
            )

        # 2. Forge the Knowledge Node (The Tickets)
        knowledge_name = "KnowledgeNode"
        if not self.client.collections.exists(knowledge_name):
            print(f"Constructing '{knowledge_name}'...")
            self.client.collections.create(
                name=knowledge_name,
                vector_config=Configure.VectorIndex.hnsw(),
                properties=[
                    Property(name="source_id", data_type=DataType.TEXT, skip_vectorization=True),
                    Property(name="data_type", data_type=DataType.TEXT), 
                    Property(name="subject", data_type=DataType.TEXT),
                    Property(name="content", data_type=DataType.TEXT),
                    Property(name="url", data_type=DataType.TEXT, skip_vectorization=True),
                    Property(name="status", data_type=DataType.INT, skip_vectorization=True),
                    Property(name="priority", data_type=DataType.INT, skip_vectorization=True),
                    Property(name="tags", data_type=DataType.TEXT_ARRAY, skip_vectorization=True),
                    Property(name="attachment_urls", data_type=DataType.TEXT_ARRAY, skip_vectorization=True),
                    Property(name="is_distilled", data_type=DataType.BOOL, default_value=False)
                ],
                references=[
                    ReferenceProperty(name="hasSourceDocument", target_collection=doc_name)
                ]
            )
            
        # 3. The Entity Graph Collection
        entGraph = "EntityGraph"
        if not self.client.collections.exists(entGraph):
            print(f"Constructing '{entGraph}'...")
            self.client.collections.create(
                name=entGraph,
                vector_config=Configure.VectorIndex.hnsw(),
                properties=[
                    Property(name="subject", data_type=DataType.TEXT),
                    Property(name="predicate", data_type=DataType.TEXT),
                    Property(name="object", data_type=DataType.TEXT),
                    Property(name="evidence_id", data_type=DataType.TEXT),
                ]
            )

        # 4. Forge the Interaction Log (The Feedback Loop)
        log_name = "InteractionLog"
        if not self.client.collections.exists(log_name):
            print(f"Constructing '{log_name}'...")
            self.client.collections.create(
                name=log_name,
                vector_config=Configure.VectorIndex.hnsw(),
                properties=[
                    Property(name="query", data_type=DataType.TEXT),
                    Property(name="answer", data_type=DataType.TEXT),
                    Property(name="timestamp", data_type=DataType.DATE),
                    Property(name="referenced_doc_ids", data_type=DataType.TEXT_ARRAY),
                    Property(name="was_successful", data_type=DataType.BOOL),
                ]
            )
            print("--- [MODERN SCHEMA INITIALIZED] ---")
        else:
            print(f"Collections are already live")

    def close(self):
        self.client.close()

if __name__ == "__main__":
    db = DatabaseManager()
    try:
        db.initialize_schema()
    finally:
        db.close()