# DocuMind - Multi-Tenant Enterprise Knowledge Assistant (RAG Platform) using .NET core and Angular

Upload documents, ask questions in natural language, and get streamed answers with page-level citations, grounded only in your data.
Supported document types:
- PDF
- DOCX
- MD
- TXT
- HTML
---
### Architecture
<img width="1266" height="995" alt="image" src="https://github.com/user-attachments/assets/0e277efd-cf71-4a6f-af26-c2088e7828c3" />


---
RAG query flow

- Receive the question and chat history.
- Rewrite the follow-up into a standalone query (LLM call).
- Embed the query.
- Run vector search (top 20) and keyword search (top 20) in parallel.
- Merge with RRF and optionally rerank (cross-encoder or LLM reranker).
- Select the top 5–8 chunks within a token budget.
- Build a grounded prompt (system rules, context with chunk IDs, question).
- Stream the answer over SSE, and parse the citations from it.
- Persist the message, citations, token usage, and latency.
