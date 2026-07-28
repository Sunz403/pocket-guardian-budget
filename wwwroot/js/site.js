(function () {
    const statusBadge = document.getElementById("aiModelStatus");
    const statusDetails = document.getElementById("aiModelStatusDetails");

    if (!statusBadge || !statusDetails) {
        return;
    }

    fetch("/health", { headers: { "Accept": "application/json" } })
        .then(function (response) {
            if (!response.ok) {
                throw new Error("Health endpoint returned " + response.status);
            }

            return response.json();
        })
        .then(function (health) {
            const isHealthy = health.status === "Healthy";
            statusBadge.className = "badge " + (isHealthy ? "text-bg-success" : "text-bg-warning");
            statusBadge.textContent = isHealthy ? "Running" : health.status;

            const localAiCheck = Array.isArray(health.checks)
                ? health.checks.find(function (check) { return check.name === "local_ai_model"; })
                : null;

            statusDetails.textContent = localAiCheck?.description || "AI health check completed.";
        })
        .catch(function () {
            statusBadge.className = "badge text-bg-danger";
            statusBadge.textContent = "Unavailable";
            statusDetails.textContent = "Could not reach the AI health endpoint.";
        });
})();
