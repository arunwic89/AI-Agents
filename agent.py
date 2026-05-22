import os
from dotenv import load_dotenv
from openai import AzureOpenAI

load_dotenv()

client = AzureOpenAI(
    api_key=os.getenv("AZURE_OPENAI_API_KEY"),
    azure_endpoint=os.getenv("AZURE_OPENAI_ENDPOINT"),
    api_version=os.getenv("AZURE_OPENAI_API_VERSION", "2024-10-21"),
)
DEPLOYMENT = os.getenv("AZURE_OPENAI_DEPLOYMENT")

def ask(question, documents, chat_history):
    history_text = ""
    for msg in chat_history[:-1]:
        role = "User" if msg["role"] == "user" else "Assistant"
        history_text += f"{role}: {msg['content']}\n"

    prompt = f"""You are a helpful assistant that answers questions strictly 
from the job description documents provided below.

Rules:
- If multiple documents exist and the question is unclear about which job, 
  ask the user to clarify which role they mean.
- Search carefully through ALL sections (Qualifications, Responsibilities, 
  Job description, Benefits) to find relevant information.
- If the answer is truly not in the documents, say 
  "I could not find that in the documents."
- Always mention which company/role your answer is about.

Documents:
{documents}

Previous conversation:
{history_text}

Current question: {question}
"""
    response = client.chat.completions.create(
        model=DEPLOYMENT,
        messages=[{"role": "user", "content": prompt}]
    )
    return response.choices[0].message.content