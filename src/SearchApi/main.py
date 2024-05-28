import os
import zipfile
import requests
import pandas as pd
import numpy as np
from flask import Flask, request, jsonify
from sentence_transformers import SentenceTransformer
import chromadb
import re

app = Flask(__name__)

SCRIPTURES_URL = "https://github.com/beandog/lds-scriptures/archive/2020.12.08.zip"
SCRIPTURES_ZIP_PATH = "./lds-scriptures.zip"
SCRIPTURES_EXTRACT_PATH = "./lds-scriptures-2020.12.08"
SCRIPTURES_CSV_PATH = f"{SCRIPTURES_EXTRACT_PATH}/csv/lds-scriptures.csv"
EMBEDDINGS_CSV_PATH = "./embeddings/lds-scriptures-chunked-embeddings.csv"
MODEL_PATH = 'models/all-MiniLM-L6-v2'

# Initialize the model
if not os.path.exists(MODEL_PATH):
    model = SentenceTransformer('all-MiniLM-L6-v2')
    model.save(MODEL_PATH)
else:
    model = SentenceTransformer(MODEL_PATH)

# Initialize ChromaDB client
chroma_client = chromadb.HttpClient(host='chroma', port=8000)

# Create a collection
collection = chroma_client.get_or_create_collection('scriptures')

# Download and unzip the scriptures
def download_and_unzip_scriptures():
    if not os.path.exists(SCRIPTURES_EXTRACT_PATH):
        app.logger.info("Downloading and unzipping the scriptures...")
        # Download the file
        response = requests.get(SCRIPTURES_URL)
        with open(SCRIPTURES_ZIP_PATH, 'wb') as file:
            file.write(response.content)
        
        # Unzip the file
        with zipfile.ZipFile(SCRIPTURES_ZIP_PATH, 'r') as zip_ref:
            zip_ref.extractall("./")
        app.logger.info("Scriptures downloaded and unzipped.")

# Create embeddings from the scriptures
def create_embeddings():
    if not os.path.exists(EMBEDDINGS_CSV_PATH):
        app.logger.info("Creating embeddings...")
        df = pd.read_csv(SCRIPTURES_CSV_PATH)
        
        def chunk_text(text, chunk_size=512, overlap=128):
            words = text.split()
            chunks = []
            for i in range(0, len(words), chunk_size - overlap):
                chunk = ' '.join(words[i:i + chunk_size])
                if chunk:
                    chunks.append(chunk)
                if i + chunk_size >= len(words):
                    break
            return chunks
        
        embedding_data = []
        for index, row in df.iterrows():
            if index % 1000 == 0:
                app.logger.info(f'Progress: {index / len(df) * 100:.2f}%')
            
            scripture_text = row['scripture_text']
            chunks = chunk_text(scripture_text)
            for chunk_index, chunk in enumerate(chunks):
                embedding = model.encode(chunk).tolist()
                new_row = row.to_dict()
                new_row['chunk_id'] = f'{index}_{chunk_index}'
                new_row['chunk_text'] = chunk
                new_row['embedding'] = embedding
                embedding_data.append(new_row)
        
        embedding_df = pd.DataFrame(embedding_data)
        embedding_df.to_csv(EMBEDDINGS_CSV_PATH, index=False)
        app.logger.info("Embeddings created and saved.")

# Load embeddings into ChromaDB
def load_embeddings_to_chromadb():
    embedding_df = pd.read_csv(EMBEDDINGS_CSV_PATH)
    embedding_df['chunk_id'] = embedding_df.index
    embedding_df['embedding'] = embedding_df['embedding'].apply(lambda x: np.fromstring(x.strip('[]'), sep=','))
    
    def get_metadata(row):
        return {
            "volume_id": row['volume_id'],
            "book_id": row['book_id'],
            "chapter_id": row['chapter_id'],
            "verse_id": row['verse_id'],
            "volume_title": row['volume_title'],
            "book_title": row['book_title'],
            "volume_long_title": row['volume_long_title'],
            "book_long_title": row['book_long_title'],
            "volume_subtitle": row['volume_subtitle'],
            "book_subtitle": row['book_subtitle'],
            "volume_short_title": row['volume_short_title'],
            "book_short_title": row['book_short_title'],
            "volume_lds_url": row['volume_lds_url'],
            "book_lds_url": row['book_lds_url'],
            "chapter_number": row['chapter_number'],
            "verse_number": row['verse_number'],
            "scripture_text": row['scripture_text'],
            "verse_title": row['verse_title'],
            "verse_short_title": row['verse_short_title'],
            "chunk_id": row['chunk_id'],
        }
    
    def add_data_in_batches(df, batch_size):
        for start in range(0, len(df), batch_size):
            end = min(start + batch_size, len(df))
            batch_df = df.iloc[start:end]
            collection.add(
                documents=[row["chunk_text"] for index, row in batch_df.iterrows()], 
                embeddings=[row["embedding"].tolist() for index, row in batch_df.iterrows()],
                metadatas=[get_metadata(row) for index, row in batch_df.iterrows()],
                ids=[str(row["chunk_id"]) for index, row in batch_df.iterrows()]
            )
    
    add_data_in_batches(embedding_df, batch_size=10000)
    app.logger.info("Embeddings loaded into ChromaDB.")

# Function to query the database based on a text query
def query_chromadb(text_query, max_results=30):
    quoted_phrases = re.findall(r'"([^"]*)"', text_query)
    text_query = text_query.replace('"', '')
    query_embedding = model.encode(text_query).tolist()

    if len(quoted_phrases) == 0:
        results = collection.query(query_embedding, n_results=max_results)
    elif len(quoted_phrases) == 1:
        results = collection.query(query_embedding, n_results=max_results, where_document={'$contains': quoted_phrases[0]})
    else:
        results = collection.query(query_embedding, n_results=max_results, where_document={"$and": [{'$contains': phrase} for phrase in quoted_phrases]})
    return results

@app.route('/search', methods=['GET'])
def search():
    query = request.args.get('query')
    threshold = request.args.get('threshold')
    max_results = int(request.args.get('max_results', 30))

    book_of_mormon_enabled = request.args.get('bom', 'true').lower() in ['true', '1']
    doctrine_and_covenants_enabled = request.args.get('dc', 'true').lower() in ['true', '1']
    old_testament_enabled = request.args.get('ot', 'true').lower() in ['true', '1']
    new_testament_enabled = request.args.get('nt', 'true').lower() in ['true', '1']

    if not query:
        return jsonify({"error": "Query parameter is required"}), 400
    
    results = query_chromadb(query, max_results=max_results)
    distances = results['distances'][0]
    metadatas = results['metadatas'][0]

    for i in range(len(metadatas)):
        metadatas[i]['distance'] = distances[i]
    
    metadatas = sorted(metadatas, key=lambda x: x['distance'])

    if threshold:
        threshold = float(threshold)
        metadatas = [metadata for metadata in metadatas if metadata['distance'] <= threshold]

    if not book_of_mormon_enabled:
        metadatas = [metadata for metadata in metadatas if metadata['volume_title'] != 'Book of Mormon']
    if not doctrine_and_covenants_enabled:
        metadatas = [metadata for metadata in metadatas if metadata['volume_title'] not in ['Doctrine and Covenants', 'Pearl of Great Price']]
    if not old_testament_enabled:
        metadatas = [metadata for metadata in metadatas if metadata['volume_title'] != 'Old Testament']
    if not new_testament_enabled:
        metadatas = [metadata for metadata in metadatas if metadata['volume_title'] != 'New Testament']

    return jsonify(metadatas)

if __name__ == '__main__':
    app.logger.info("Starting the Search API")
    download_and_unzip_scriptures()
    create_embeddings()
    load_embeddings_to_chromadb()

    app.logger.info("Search API started successfully.")
    app.run(host='0.0.0.0', port=5000, debug=True)
