let exceptionRows = [];

async function loadExceptions() {
  const page = await Algos.api("/api/exceptions?pageSize=500");
  exceptionRows = page.items || [];
  renderExceptions();
}

function renderExceptions() {
  const search = Algos.$("#searchInput").value.trim().toLowerCase();
  const rows = exceptionRows.filter(row => !search || JSON.stringify(row).toLowerCase().includes(search));
  Algos.$("#exceptionCount").textContent = `${rows.length} rows`;
  Algos.$("#exceptionTable").innerHTML = Algos.table(["Time", "Type", "Message", "Severity", "Trace"], rows.map(row => `
    <tr>
      <td>${Algos.time(row.timestampUtc)}</td>
      <td>${Algos.escapeHtml(row.exceptionType)}</td>
      <td>${Algos.escapeHtml(row.message)}</td>
      <td><span class="badge bad">${Algos.escapeHtml(row.severity)}</span></td>
      <td><button class="copy mono" data-copy="${Algos.escapeHtml(row.traceId)}">${Algos.short(row.traceId)}</button></td>
    </tr>`).join(""));
  Algos.bindActions();
}

Algos.$("#searchInput").addEventListener("input", renderExceptions);
Algos.startDashboard(loadExceptions);
