FROM pgvector/pgvector:pg16
COPY postgres-production-entrypoint.sh /usr/local/bin/postgres-production-entrypoint.sh
RUN chmod 0755 /usr/local/bin/postgres-production-entrypoint.sh
ENTRYPOINT ["/usr/local/bin/postgres-production-entrypoint.sh"]
