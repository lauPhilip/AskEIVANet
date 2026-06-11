# 📝 AskEIVA Project Roadmap & TODO List

This document tracks upcoming feature implementations, system enhancements, and architectural refinements for the AskEIVA platform.

---

## 🟩 Completed Milestones
* **Architectural Migration:** Successfully moved legacy Python/Streamlit prototype to a decoupled C# .NET 10 Clean Architecture layout.
* **Asynchronous Scraper Implementation:** Built a dynamic HTML parsing stream using `HtmlAgilityPack` to safely extract text from public solution directories.
* **Idempotency Protections:** Safeguarded the administrative UI by dynamically disabling synchronization buttons when Weaviate collections are populated.
* **Asset Synchronization:** Fixed cache-busting behaviors for UI static files (favicons, themes) to guarantee immediate hot-reload distribution.

---

## 🟨 High Priority: Core App Improvements
- [ ] **TextSplitter Unit Tests:** Implement comprehensive `xUnit` test suites for `TextSplitter.cs` to validate sliding window index splits against edge-case technical strings (use the old technic for chunking /revisit).
- [ ] **Error Logging Decoupling:** Introduce a structured logging decorator using Serilog in the Infrastructure layer to capture pipeline faults cleanly.
- [ ] **Weaviate Retry Policies:** Implement resilient transient fault handling using `Polly` inside Weaviate repositories for multi-threaded batch operations.
- [ ] **Sliding Window Tuning:** Fine-tune character overlaps (`chunkSize` and `chunkOverlap` settings) specifically for short system fault logs and parameter registers.

---

## 🟦 Medium Priority: Feature Enhancements
- [ ] **Context Graph RAG Optimization:** Refactor to pass advanced semantic context weights to a more unified model.
- [ ] **Hybrid Search Balancing:** Implement a runtime slider in the WebUI to adjust the vector-keyword balance (`alpha` parameter) dynamically in `SearchKnowledgeQuery.cs`.
- [ ] **Evaluation Trace Metrics:** Expand the `EvaluationDashboard.razor` to visually chart automated pipeline test score histories over time.
- [ ] **Jira Content Parsing:** Harden `AtlassianDocumentParser.cs` to fully reconstruct complex nested tables from raw ADF payloads (JIRA IS NOT WORKING ATM).
- [ ] **Workflow page with custom ai flows:** moving towards autonomous AI and giving the employees oppurtunity to create their own AI flows.
- [ ] **Train a specific llm model on the rlhf training data:** Because of the rich data try to train a llm on eivas own param.

---

## 🟧 Low Priority: DevOps & Distribution
- [ ] **CI/CD Build Pipeline:** Configure a GitHub Action to automate linting, solution building, and unit test execution on pull requests targeting `master`.
- [ ] **Environment Validation on Startup:** Upgrade `WeaviateSchemaProvisioner.cs` to gracefully fail and log specific connection issues if API keys are missing.
- [ ] **Video Walkthrough Archiving:** Record side-by-side execution runs between `master` and the `feature/video-demo` branch to present architectural scaling to the EIVA engineering team.