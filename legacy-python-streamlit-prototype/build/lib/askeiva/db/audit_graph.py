from src.askeiva.core.agent import AskEIVA

def audit_graph():
    agent = AskEIVA()
    graph = agent.client.collections.get("EntityGraph")
    
    print(f"\n--- [ ENTITY GRAPH AUDIT ] ---")
    # Fetch the last 20 triples created
    response = graph.query.fetch_objects(limit=50)
    
    if not response.objects:
        print("The graph is empty. Check your distiller logic.")
        return

    for obj in response.objects:
        props = obj.properties
        # Printing in a readable format: [Subject] --(Predicate)--> [Object]
        print(f"🔗 [{props.get('subject')}] --({props.get('predicate')})--> [{props.get('object')}]")
    
    print(f"--- [ TOTAL TRIPLES IN GRAPH: {len(response.objects)} ] ---\n")
    agent.close()

if __name__ == "__main__":
    audit_graph()