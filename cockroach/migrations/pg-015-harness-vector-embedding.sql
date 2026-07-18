-- Enable pgvector extension (idempotent)
CREATE EXTENSION IF NOT EXISTS vector;

-- Add embedding column to harness memory_entries table
-- 256-dim vector for semantic similarity search
ALTER TABLE harness.memory_entries ADD COLUMN IF NOT EXISTS embedding vector(256);

-- Index for approximate nearest neighbor search (optional, for performance)
-- CREATE INDEX IF NOT EXISTS ix_memory_entries_embedding ON harness.memory_entries
--   USING ivfflat (embedding vector_cosine_ops) WITH (lists = 100);
