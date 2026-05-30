async function loadOverview() {
  const [overview, analytics, requests] = await Promise.all([
    Algos.api("/api/overview"),
    Algos.api("/api/analytics"),
    Algos.api("/api/requests?pageSize=12")
  ]);
  Algos.$("#kpiGrid").innerHTML = [
    Algos.kpi("Requests", Algos.number(overview.total), "Captured in retention window"),
    Algos.kpi("Error Rate", `${overview.errorRate}%`, `${Algos.number(overview.errors)} server errors`),
    Algos.kpi("Avg Latency", `${overview.avg}ms`, "Mean response time"),
    Algos.kpi("P95", `${overview.p95}ms`, `P50 ${overview.p50}ms`),
    Algos.kpi("P99", `${overview.p99}ms`, "Tail latency"),
    Algos.kpi("Modules", Algos.number((analytics.modules || []).length), "Tracked usage groups")
  ].join("");
  Algos.renderMetricList("#moduleUsage", analytics.modules || [], "name", "count", "avgMs", "avg");
  Algos.renderMetricList("#leastUsage", analytics.leastUsed || [], "name", "count", "users", "users");
  Algos.renderMetricList("#slowEndpoints", overview.slowest || [], "path", "durationMs", "statusCode", "status");
  Algos.renderMetricList("#dependencySummary", overview.dependencies || [], "name", "avgMs", "failures", "failures");
  Algos.renderMetricList("#statusDistribution", overview.statusCodes || [], "statusCode", "count", "count", "requests");
  renderHealth(overview);
  renderCritical(overview.recentCritical || []);
  Algos.$("#recentRequests").innerHTML = Algos.table(["Time", "Method", "Path", "Status", "Duration", "Trace"], (requests.items || []).map(row => `
    <tr>
      <td>${Algos.time(row.timestampUtc)}</td>
      <td><span class="badge">${Algos.escapeHtml(row.method)}</span></td>
      <td><button class="copy" data-open="${row.id}">${Algos.escapeHtml(row.path)}</button></td>
      <td>${Algos.statusBadge(row.statusCode)}</td>
      <td class="mono">${row.durationMs}ms</td>
      <td><button class="copy mono" data-copy="${Algos.escapeHtml(row.traceId)}">${Algos.short(row.traceId)}</button></td>
    </tr>`).join(""));
  Algos.bindActions();
}

function renderHealth(overview) {
  const score = Math.max(0, Math.round(100 - (overview.errorRate * 8) - Math.max(0, overview.p95 - 500) / 20));
  Algos.$("#healthScore").textContent = score;
  Algos.$("#healthTitle").textContent = score >= 90 ? "Systems look healthy" : score >= 70 ? "Some pressure detected" : "Attention needed";
  Algos.$("#healthSummary").textContent = `${overview.total} requests, ${overview.errorRate}% error rate, ${overview.p95}ms p95 latency.`;
}

function renderCritical(rows) {
  Algos.$("#criticalIssues").innerHTML = rows.length ? rows.map(row => `
    <div class="metric-row">
      <div class="metric-row-head"><strong>${Algos.escapeHtml(row.exceptionType)}</strong><span class="badge bad">${Algos.escapeHtml(row.severity)}</span></div>
      <div>${Algos.escapeHtml(row.message)}</div>
      <button class="copy mono" data-copy="${Algos.escapeHtml(row.traceId)}">${Algos.short(row.traceId)}</button>
    </div>`).join("") : Algos.empty("No critical exceptions in the current retention window.");
  Algos.bindActions();
}

Algos.startDashboard(loadOverview);
