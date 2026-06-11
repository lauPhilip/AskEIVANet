import json

class EIVAKnowledgeGraph:
    def __init__(self, mistral_client):
        self.mistral = mistral_client

    def distill_ticket_to_triples(self, ticket_text: str):
        """Extracts Entities, Structures, and Relations to build a technical map."""
        prompt = (
            "You are a Senior EIVA Systems Architect. Analyze the technical support text "
            "to extract a formal knowledge graph. Focus on:\n"
            "1. ENTITIES: Hardware models, Software, Error Codes, Serial numbers.\n"
            "2. STRUCTURES: [Part A] IS_COMPONENT_OF [System B], [Version X] IS_VERSION_OF [Software Y].\n"
            "3. RELATIONS: [Issue] FIXED_BY [Action], [Software] INCOMPATIBLE_WITH [OS].\n\n"
            "Output ONLY a JSON object: {'relations': [{'source': '', 'relation': '', 'target': ''}]}"
        )
        
        response = self.mistral.chat.complete(
            model="mistral-large-latest",
            response_format={"type": "json_object"},
            messages=[{"role": "user", "content": f"{prompt}\n\nTicket: {ticket_text}"}]
        )
        return json.loads(response.choices[0].message.content)