import time
from tqdm import tqdm
from weaviate.classes.query import Filter
from src.askeiva.core.agent import AskEIVA
from src.askeiva.core.graph_engine import EIVAKnowledgeGraph

class KnowledgeDistiller:
    def __init__(self):
        self.agent = AskEIVA()
        self.graph_engine = EIVAKnowledgeGraph(self.agent.mistral)
        # Collection References
        self.tickets = self.agent.client.collections.get("KnowledgeNode")
        self.graph = self.agent.client.collections.get("EntityGraph")

    def process_tickets(self, limit=100):
        """Processes only un-distilled tickets into the EntityGraph."""
        print(f"--- [ STARTING DISTILLATION BATCH: {limit} TICKETS ] ---")
        
        try:
            # 1. Fetch only tickets where is_distilled is False or None
            # Weaviate v4: Use the Filter class
            response = self.tickets.query.fetch_objects(
                limit=limit,
                filters= 
                        Filter.by_property("is_distilled").equal(False)
            )
            
            if not response.objects:
                print("🏁 All tickets have been distilled. Knowledge Graph is fully evolved.")
                return

            for ticket in tqdm(response.objects, desc="Evolving Knowledge Graph"):
                content = ticket.properties.get("content", "")
                ticket_id = ticket.uuid
                
                if not content or len(content) < 50:
                    # Mark empty/useless tickets as distilled so we don't keep checking them
                    self.tickets.data.update(uuid=ticket_id, properties={"is_distilled": True})
                    continue

                try:
                    # 2. Reasoning: Distill raw text into structured Triples
                    triples = self.graph_engine.distill_ticket_to_triples(content)
                    
                    # 3. Store Relations in EntityGraph
                    for rel in triples.get("relations", []):
                        self.graph.data.insert(
                            properties={
                                "subject": str(rel["source"]),
                                "predicate": str(rel["relation"]),
                                "object": str(rel["target"]),
                                "evidence_id": str(ticket_id)
                            }
                        )
                    
                    # 4. CRITICAL: Mark this ticket as processed
                    self.tickets.data.update(
                        uuid=ticket_id,
                        properties={"is_distilled": True}
                    )
                    
                    # Respect rate limits
                    time.sleep(0.6)
                    
                except Exception as e:
                    print(f"\n[!] Skipping ticket {ticket_id} due to error: {e}")

            print(f"--- [ BATCH COMPLETE: {len(response.objects)} TICKETS PROCESSED ] ---")
            
        finally:
            self.agent.close()

if __name__ == "__main__":
    distiller = KnowledgeDistiller()
    # Adjust this number based on how much 'brain power' you want to add today
    distiller.process_tickets(limit=100)