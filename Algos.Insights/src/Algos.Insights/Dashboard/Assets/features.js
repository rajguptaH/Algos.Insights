async function loadFeatures() {
  const [analytics, page] = await Promise.all([
    Algos.api("/api/analytics"),
    Algos.api("/api/features?pageSize=500")
  ]);
  const rows = page.items || [];
  Algos.$("#featureKpis").innerHTML = [
    Algos.kpi("Feature Events", Algos.number(rows.length), "Tracked feature hits"),
    Algos.kpi("Modules", Algos.number((analytics.modules || []).length), "Unique modules"),
    Algos.kpi("Least Used", Algos.number((analytics.leastUsed || []).length), "Low usage groups"),
    Algos.kpi("Users", Algos.number((analytics.modules || []).reduce((sum, x) => sum + (x.users || 0), 0)), "Known users")
  ].join("");
  Algos.$("#featureCount").textContent = `${rows.length} rows`;
  Algos.renderMetricList("#moduleUsage", analytics.modules || [], "name", "count", "avgMs", "avg");
  Algos.renderMetricList("#leastUsage", analytics.leastUsed || [], "name", "count", "users", "users");
  Algos.$("#featureTable").innerHTML = Algos.table(["Time", "Module", "Feature", "User", "Duration", "Trace"], rows.map(row => `
    <tr>
      <td>${Algos.time(row.timestampUtc)}</td>
      <td><strong>${Algos.escapeHtml(row.moduleName)}</strong></td>
      <td>${Algos.escapeHtml(row.featureName)}</td>
      <td>${Algos.escapeHtml(row.userId || "Anonymous")}</td>
      <td>${Algos.fmt(row.durationMs)}${row.durationMs ? "ms" : ""}</td>
      <td><button class="copy mono" data-copy="${Algos.escapeHtml(row.traceId)}">${Algos.short(row.traceId)}</button></td>
    </tr>`).join(""));
  Algos.bindActions();
}

Algos.startDashboard(loadFeatures);
