import os
import httpx
import weaviate
from datetime import datetime
from weaviate.classes.query import HybridFusion
from mistralai.client import Mistral
from dotenv import load_dotenv

load_dotenv()

class AskEIVA:
    def __init__(self):
        self.client = weaviate.connect_to_weaviate_cloud(
            cluster_url=os.getenv("WEAVIATE_URL"),
            auth_credentials=weaviate.auth.AuthApiKey(os.getenv("WEAVIATE_API_KEY")),
            headers={"X-Mistral-Api-Key": os.getenv("MISTRAL_API_KEY")}
        )
        self.http_client = httpx.Client(timeout=120.0)
        self.mistral = Mistral(api_key=os.getenv("MISTRAL_API_KEY"), client=self.http_client)
        
        # Collections
        self.tickets = self.client.collections.get("KnowledgeNode")
        self.docs = self.client.collections.get("DocumentLibrary")
        self.logs = self.client.collections.get("InteractionLog")
        self.graph = self.client.collections.get("EntityGraph")

    def search(self, query: str):
        tix = self.tickets.query.hybrid(query=query, limit=3, fusion_type=HybridFusion.RELATIVE_SCORE)
        docs = self.docs.query.hybrid(query=query, limit=3, fusion_type=HybridFusion.RELATIVE_SCORE)
        return tix.objects, docs.objects

    def action_router(self, query: str):
        """Checks the EntityGraph for specific 'FIXED_BY' or 'HAS_ISSUE' relations."""
        graph_hints = []
        try:
            # Look for direct triples related to the query
            results = self.graph.query.hybrid(query=query, limit=5)
            for obj in results.objects:
                p = obj.properties
                # We specifically look for these predicates to provide 'Action' advice
                if p.get("predicate") in ["FIXED_BY", "REQUIRES_ACTION", "HAS_ISSUE", "COMPATIBLE_WITH"]:
                    hint = f"Graph Logic: {p['subject']} -> {p['predicate']} -> {p['object']}"
                    graph_hints.append(hint)
        except Exception as e:
            print(f"Router Error: {e}")
        return graph_hints

    def get_sources(self, query: str):
        tix, docs = self.search(query)
        sources = []
        seen = set()
        for d in docs:
            title = d.properties.get("title", "Manual")
            if title not in seen:
                sources.append({"type": "Manual", "title": title, "url": d.properties.get("url", "#")})
                seen.add(title)
        for t in tix:
            subj = t.properties.get("subject", "Ticket")
            if subj not in seen:
                sources.append({"type": "Ticket", "title": subj, "url": t.properties.get("url", "#")})
                seen.add(subj)
        return sources

    def stream_answer(self, query: str):
        """Generates a response informed by both Documents and the Entity Graph."""
        tix, docs = self.search(query)
        graph_hints = self.action_router(query)
        
        context_blocks = [f"DOC [{d.properties.get('title')}]: {d.properties.get('content', '')[:3000]}" for d in docs]
        context_blocks += [f"TICKET [{t.properties.get('subject')}]: {t.properties.get('content', '')[:3000]}" for t in tix]
        
        hint_text = "\n".join(graph_hints) if graph_hints else "No direct historical fixes found in graph."
        
        try:
            stream = self.mistral.chat.stream(
                model="mistral-large-latest",
                messages=[
                    {
                        "role": "system", 
                        "content": (
                            "You are askEIVA, technical lead. Use context AND graph hints. "
                            f"HISTORICAL GRAPH FIXES:\n{hint_text}"
                        )
                    },
                    {"role": "user", "content": f"QUERY: {query}\n\nCONTEXT:\n{chr(10).join(context_blocks)}"}
                ]
            )

            full_response = ""
            for chunk in stream:
                content = chunk.data.choices[0].delta.content
                if content:
                    full_response += content
                    yield content

            # Log interaction
            ref_ids = [str(obj.uuid) for obj in docs] + [str(obj.uuid) for obj in tix]
            self.logs.data.insert(
                properties={
                    "query": query, "answer": full_response,
                    "timestamp": datetime.now().astimezone(),
                    "referenced_doc_ids": ref_ids, "was_successful": True 
                }
            )
        except Exception as e:
            yield f"EIVA Intelligence Error: {str(e)}"

    def close(self):
        self.client.close()
        self.http_client.close()