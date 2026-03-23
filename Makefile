.PHONY: up down produce consume verify logs

# ── Infrastructure ────────────────────────────────────────────────────────────
up:
	@echo "► Starting Kafka + Redis…"
	docker compose up -d
	@echo "► Waiting for Kafka to be ready (up to 60 s)…"
	@for i in $$(seq 1 12); do \
	  docker compose exec kafka kafka-topics.sh --bootstrap-server localhost:9092 --list > /dev/null 2>&1 && echo "  Kafka is ready." && break; \
	  echo "  Waiting… ($$i/12)"; sleep 5; \
	done

down:
	@echo "► Tearing down…"
	docker compose down -v

logs:
	docker compose logs -f

# ── Application ───────────────────────────────────────────────────────────────
produce:
	@echo "► Producing test data to Kafka…"
	dotnet run -- --produce

consume:
	@echo "► Starting consumer (Ctrl-C to stop after messages are processed)…"
	dotnet run

# ── Verification ──────────────────────────────────────────────────────────────
verify:
	@echo ""
	@echo "═══════════════════════════════════════════════════════"
	@echo "  Redis hash: sbmm:987654321:3  (platformId=3, acct=987654321)"
	@echo "  Expected: playtime:normalized_value=0.91 (updated from 0.45)"
	@echo "            wins:normalized_value=0.72"
	@echo "═══════════════════════════════════════════════════════"
	docker compose exec redis redis-cli HGETALL sbmm:987654321:3
	@echo ""
	@echo "═══════════════════════════════════════════════════════"
	@echo "  Redis hash: sbmm:123456789:5  (platformId=5, acct=123456789)"
	@echo "  Expected: playtime:normalized_value=0.88"
	@echo "═══════════════════════════════════════════════════════"
	docker compose exec redis redis-cli HGETALL sbmm:123456789:5
	@echo ""
	@echo "═══════════════════════════════════════════════════════"
	@echo "  Redis hash: sbmm:555000111:7  (platformId=7, acct=555000111)"
	@echo "  Expected: playtime:normalized_value=0.33"
	@echo "═══════════════════════════════════════════════════════"
	docker compose exec redis redis-cli HGETALL sbmm:555000111:7

# ── Full end-to-end (single command) ─────────────────────────────────────────
test: up produce
	@echo "► Running consumer for 8 s then verifying Redis…"
	timeout 8 dotnet run || true
	@$(MAKE) verify
