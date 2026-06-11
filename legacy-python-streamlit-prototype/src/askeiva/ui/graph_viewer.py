import streamlit as st
from pyvis.network import Network
import streamlit.components.v1 as components
from src.askeiva.core.agent import AskEIVA

def render_brain_graph():
    st.title("🧠 askEIVA Graph")
    
    # Use your existing agent to talk to Weaviate
    agent = AskEIVA()
    try:
        graph_collection = agent.client.collections.get("EntityGraph")
        # Pull the last 300 extractions (matches your current ticket count)
        response = graph_collection.query.fetch_objects(limit=300)
        
        # Physics-based Network setup
        net = Network(height="600px", width="100%", bgcolor="#1e1e1e", font_color="white", directed=True)
        
        for obj in response.objects:
            s, p, o = obj.properties.get("subject"), obj.properties.get("predicate"), obj.properties.get("object")
            
            # Nodes: Cyan for Subjects, Purple for Objects
            net.add_node(s, label=s, color="#00e5ff", size=15)
            net.add_node(o, label=o, color="#bb86fc", size=15)
            net.add_edge(s, o, title=p, label=p, color="#555555")

        net.toggle_physics(True)
        html_file = "temp_graph.html"
        net.save_graph(html_file)

        with open(html_file, 'r', encoding='utf-8') as f:
            components.html(f.read(), height=650)
            
    finally:
        agent.close()

if __name__ == "__main__":
    render_brain_graph()