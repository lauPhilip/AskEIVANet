import streamlit as st
import pandas as pd
from src.askeiva.core.agent import AskEIVA
from src.askeiva.ui.graph_viewer import render_brain_graph

# --- EIVA BRANDING ---
EIVA_BLUE = "#003366"
EIVA_YELLOW = "#FFCC00"
EIVA_GRAY = "#F8F9FA"

st.set_page_config(page_title="askEIVA", page_icon="⚓", layout="wide")

# CSS Update: Removed sidebar hiding to allow navigation
st.markdown(f"""
    <style>
    .stApp {{ background-color: #FFFFFF; }}
    .eiva-title {{ color: {EIVA_BLUE}; font-family: 'Helvetica Neue', sans-serif; }}
    .stChatMessage {{ background-color: {EIVA_GRAY}; border-radius: 8px; }}
    div.stButton > button {{ border: 1px solid {EIVA_BLUE}; color: {EIVA_BLUE}; }}
    div.stButton > button:hover {{ border: 1px solid {EIVA_YELLOW}; color: {EIVA_YELLOW}; }}
    /* Sidebar styling */
    [data-testid="stSidebar"] {{ background-color: {EIVA_BLUE}; color: white; }}
    [data-testid="stSidebar"] * {{ color: white !important; }}
    </style>
    """, unsafe_allow_html=True)

if "agent" not in st.session_state:
    st.session_state.agent = AskEIVA()

if "messages" not in st.session_state:
    st.session_state.messages = []

def get_ingestion_stats():
    """Fetches real-time counts from the KnowledgeNode collection."""
    try:
        # Get the collection
        kn_collection = st.session_state.agent.client.collections.get("KnowledgeNode")
        
        # Aggregate total count
        total_res = kn_collection.aggregate.over_all(total_count=True)
        total_count = total_res.total_count
        
        # Aggregate distilled count (where is_distilled is True)
        from weaviate.classes.query import Filter
        distilled_res = kn_collection.aggregate.over_all(
            total_count=True,
            filters=Filter.by_property("is_distilled").equal(True)
        )
        distilled_count = distilled_res.total_count
        
        return total_count, distilled_count
    except Exception:
        return 0, 0
    
# --- NAVIGATION SIDEBAR ---
with st.sidebar:
    st.markdown(f'<h2 style="color:{EIVA_YELLOW};">⚓ askEIVA Menu</h2>', unsafe_allow_html=True)
    mode = st.radio("INTERFACE SELECTOR", ["askEIVA chat", "askEIVA Knowledge Graph"])
    
    st.divider()
    
    # --- LIVE INGESTION STATS ---
    st.subheader("System Status")
    total, distilled = get_ingestion_stats()
    pending = total - distilled
        
    col_a, col_b = st.columns(2)
    with col_a:
        st.metric("Total Tickets", total)
    with col_b:
        st.metric("Processed", distilled)
            
    if total > 0:
        progress = distilled / total
        st.progress(progress, text=f"{int(progress*100)}% Distilled")
        st.caption(f"⏳ {pending} tickets in queue")
    
    st.divider()
    st.caption("EIVA Customer Sevice Agent Platform")
    st.write(f"**Operator:** John Doe")

def display_reference_table(sources):
    if sources:
        st.write("---")
        st.caption("TECHNICAL EVIDENCE POOL")
        df = pd.DataFrame(sources)
        df.columns = ["Category", "Source Title", "Resource URL"]
        st.dataframe(
            df,
            column_config={
                "Resource URL": st.column_config.LinkColumn("Access Link", display_text="View Source"),
                "Category": st.column_config.TextColumn("Type", width="small")
            },
            hide_index=True, width="stretch"
        )

def run_chat_interface():
    # --- HEADER & CLEAR ---
    col1, col2 = st.columns([0.8, 0.2])
    with col1:
        st.markdown(f'<h1 class="eiva-title">⚓ askEIVA</h1>', unsafe_allow_html=True)
    with col2:
        st.write("##")
        if st.button("🧹 Clear Deck"):
            st.session_state.messages = []
            st.rerun()

    # --- CHAT LOOP ---
    for message in st.session_state.messages:
        avatar = "👤" if message["role"] == "user" else "⚓"
        with st.chat_message(message["role"], avatar=avatar):
            st.markdown(message["content"])
            if "sources" in message:
                display_reference_table(message["sources"])

    if prompt := st.chat_input("Ask about EIVA systems or support cases..."):
        st.session_state.messages.append({"role": "user", "content": prompt})
        with st.chat_message("user", avatar="👤"):
            st.markdown(prompt)

        with st.chat_message("assistant", avatar="⚓"):
            full_response = st.write_stream(st.session_state.agent.stream_answer(prompt))
            sources = st.session_state.agent.get_sources(prompt)
            display_reference_table(sources)
            
            st.session_state.messages.append({
                "role": "assistant", "content": full_response, "sources": sources
            })

# --- MAIN ROUTING ---
if mode == "askEIVA chat":
    run_chat_interface()
else:
    render_brain_graph()