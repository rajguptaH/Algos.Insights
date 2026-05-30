let requestRows = [];

async function loadRequests() {
  const [overview, page] = await Promise.all([
    Algos.api("/api/overview"),
    Algos.api("/api/requests?pageSize=500")
  ]);
  requestRows = page.items || [];
  Algos.$("#requestKpis").innerHTML = [
    Algos.kpi("Requests", Algos.number(overview.total), "Total captured"),
    Algos.kpi("Errors", Algos.number(overview.errors), `${overview.errorRate}% rate`),
    Algos.kpi("Average", `${overview.avg}ms`, "Mean latency"),
    Algos.kpi("P95", `${overview.p95}ms`, "Tail latency")
  ].join("");
  renderRequests();
}

function renderRequests() {
  const search = Algos.$("#searchInput").value.trim().toLowerCase();
  const method = Algos.$("#methodFilter").value;
  const status = Algos.$("#statusFilter").value;
  const [sortKey, direction] = Algos.$("#sortSelect").value.split(":");
  const rows = requestRows
    .filter(row => !search || JSON.stringify(row).toLowerCase().includes(search))
    .filter(row => !method || row.method === method)
    .filter(row => !status || String(row.statusCode || "").startsWith(status))
    .sort((a, b) => Algos.compare(a, b, sortKey, direction));

  Algos.$("#requestCount").textContent = `${rows.length} rows`;
  Algos.$("#requestTable").innerHTML = Algos.table(["Time", "Method", "Path", "Status", "Duration", "Module", "Trace", ""], rows.map(row => `
    <tr>
      <td>${Algos.time(row.timestampUtc)}</td>
      <td><span class="badge">${Algos.escapeHtml(row.method)}</span></td>
      <td><button class="copy" data-open="${row.id}">${Algos.escapeHtml(row.path)}</button><div class="muted">${Algos.escapeHtml(row.queryString || "")}</div></td>
      <td>${Algos.statusBadge(row.statusCode)}</td>
      <td class="mono">${row.durationMs}ms</td>
      <td>${Algos.escapeHtml(row.module || "Unassigned")}<div class="muted">${Algos.escapeHtml(row.feature || "")}</div></td>
      <td><button class="copy mono" data-copy="${Algos.escapeHtml(row.traceId)}">${Algos.short(row.traceId)}</button></td>
      <td><button class="icon-text-button" data-open="${row.id}" type="button">Details</button></td>
    </tr>`).join(""));
  Algos.bindActions();
}

["#searchInput", "#methodFilter", "#statusFilter", "#sortSelect"].forEach(selector => {
  Algos.$(selector).addEventListener("input", renderRequests);
  Algos.$(selector).addEventListener("change", renderRequests);
});
Algos.startDashboard(loadRequests);
