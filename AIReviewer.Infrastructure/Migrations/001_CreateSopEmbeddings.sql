-- Enable pgvector extension
CREATE EXTENSION IF NOT EXISTS vector;

-- Create SOP embeddings table
CREATE TABLE IF NOT EXISTS sop_embeddings (
    id SERIAL PRIMARY KEY,
    content TEXT NOT NULL,
    source_file VARCHAR(500) NOT NULL,
    embedding vector(1536) NOT NULL
);

-- Create index for similarity search
CREATE INDEX IF NOT EXISTS idx_sop_embeddings_vector
    ON sop_embeddings USING ivfflat (embedding vector_cosine_ops)
    WITH (lists = 10);
