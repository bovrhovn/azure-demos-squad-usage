const { createApp } = Vue;
createApp({
    data: () => ({ authorization: null, message: "", error: "", loading: false }),
    methods: {
        async read(response) { const body = await response.json(); if (!response.ok) throw new Error(body.detail ?? "Request failed."); return body; },
        async begin() { this.loading = true; try { this.authorization = await this.read(await fetch(apiEndpoints.copilot.beginDeviceFlow, { method: "POST" })); } catch (error) { this.error = error.message; } finally { this.loading = false; } },
        async complete() { try { const result = await this.read(await fetch(apiEndpoints.copilot.completeDeviceFlow, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ deviceCode: this.authorization.deviceCode }) })); if (result.kind === 0) location.assign("/"); else this.message = result.kind === 1 ? "GitHub is waiting for approval." : "This activation code is no longer valid."; } catch (error) { this.error = error.message; } }
    }
}).mount("#authentication-app");
