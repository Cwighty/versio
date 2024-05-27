import os
import re
from flask import Flask, request, jsonify
import pandas as pd
from sentence_transformers import SentenceTransformer
import chromadb

app = Flask(__name__)

model_path = 'models/all-MiniLM-L6-v2'

# download the model if it is not available

if not os.path.exists(model_path):
    model = SentenceTransformer('all-MiniLM-L6-v2')
    model.save(model_path)
else:
    model = SentenceTransformer(model_path)

# Initialize ChromaDB client
chroma_client = chromadb.HttpClient(host='chroma', port=8000)

# Create a collection
collection = chroma_client.get_or_create_collection('scriptures')

# Function to query the database based on a text query
def query_chromadb(text_query, max_results=30):

    # capture quoted words or phrases
    quoted_phrases = []
    # # start at first quote
    # start = text_query.find('"')
    # while start != -1:
    #     # find the end quote
    #     end = text_query.find('"', start + 1)
    #     # if there is an end quote, add the phrase to the list
    #     if end != -1:
    #         quoted_phrases.append(text_query[start + 1:end])
    #     # find the next quote
    #     start = text_query.find('"', end + 1)

    #use regex to capture quoted phrases
    quoted_phrases = re.findall(r'"([^"]*)"', text_query)

    #parse out quoted phrases
    text_query = text_query.replace('"', '')

    query_embedding = model.encode(text_query).tolist()

    app.logger.info(f"Query: {text_query}")
    app.logger.info(f"quoted_phrases: {quoted_phrases}")

    results = None

    if len(quoted_phrases) == 0:
        results = collection.query(
            query_embedding,
            n_results=max_results
            )
        return results
    elif len(quoted_phrases) == 1:
        results = collection.query(
            query_embedding,
            n_results=max_results,
            where_document = {'$contains': quoted_phrases[0]}
            )
        return results
    else:
        results = collection.query(
            query_embedding,
            n_results=max_results,
            where_document={
                "$and" :[ {'$contains': quoted_phrase} for quoted_phrase in quoted_phrases]
            } 
            )
    return results

@app.route('/search', methods=['GET'])
def search():
    query = request.args.get('query')
    threshold = request.args.get('threshold')
    max_results = int(request.args.get('max_results'))

    book_of_mormon_enabled = request.args.get('bom', 'true').lower() in ['true', '1']
    doctrine_and_covenants_enabled = request.args.get('dc', 'true').lower() in ['true', '1']
    old_testament_enabled = request.args.get('ot', 'true').lower() in ['true', '1']
    new_testament_enabled = request.args.get('nt', 'true').lower() in ['true', '1']

    if not query:
        return jsonify({"error": "Query parameter is required"}), 400
    
    results = query_chromadb(query, max_results=max_results)

    distances = results['distances'][0]
    metatdatas = results['metadatas'][0]

    # Filter results based on threshold
    if threshold:
        threshold = float(threshold)
        distances = [distance for distance in distances if distance < threshold]
        metatdatas = metatdatas[:len(distances)]

    # Filter results based on enabled books
    if book_of_mormon_enabled == False:
        app.logger.info("filtering book of mormon")
        metatdatas = [metadata for metadata in metatdatas if metadata['volume_title'] != 'Book of Mormon']
    if doctrine_and_covenants_enabled == False:
        app.logger.info("filtering doctrine and covenants")
        metatdatas = [metadata for metadata in metatdatas if metadata['volume_title'] != 'Doctrine and Covenants']
        metatdatas = [metadata for metadata in metatdatas if metadata['volume_title'] != 'Pearl of Great Price']
    if old_testament_enabled == False:
        app.logger.info("filtering old testament")
        metatdatas = [metadata for metadata in metatdatas if metadata['volume_title'] != 'Old Testament']
    if new_testament_enabled == False:
        app.logger.info("filtering new testament")
        metatdatas = [metadata for metadata in metatdatas if metadata['volume_title'] != 'New Testament']

    return jsonify(metatdatas)

if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5000, debug=True)
