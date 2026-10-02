const { createApp } = Vue;

createApp({
    data() {
        return { modules: [], isLoading: false, error: "" };
    },
    async mounted() {
        await this.loadDashboard();
    },
    methods: {
        async loadDashboard(refresh = false) {
            this.isLoading = true;
            this.error = "";
            try {
                const response = await fetch(
                    refresh ? "/api/dashboard/modules/refresh" : "/api/dashboard/modules",
                    refresh ? { method: "POST" } : undefined);
                const body = await response.json();
                if (!response.ok) throw new Error(body.detail ?? body.title ?? "The dashboard could not be loaded.");
                this.modules = body;
            } catch (error) {
                this.error = error.message;
            } finally {
                this.isLoading = false;
            }
        }
    }
}).mount("#dashboard-app");
