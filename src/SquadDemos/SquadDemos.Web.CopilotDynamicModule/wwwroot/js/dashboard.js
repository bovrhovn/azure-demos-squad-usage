const { createApp } = Vue;

createApp({
    data() {
        return {
            modules: [],
            moduleFiles: [],
            modulePendingDeletion: "",
            isDeleting: false,
            isLoading: false,
            error: ""
        };
    },
    async mounted() {
        await this.loadDashboard();
    },
    computed: {
        moduleOrderRange() {
            if (this.modules.length === 0) return "None";
            const orders = this.modules.map(module => module.order).sort((first, second) => first - second);
            return orders.length === 1 ? String(orders[0]) : `${orders[0]} to ${orders.at(-1)}`;
        }
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
                await this.loadModuleFiles();
            } catch (error) {
                this.error = error.message;
            } finally {
                this.isLoading = false;
            }
        },
        async loadModuleFiles() {
            const response = await fetch("/api/dashboard/module-files");
            const body = await response.json();
            if (!response.ok) throw new Error(body.detail ?? body.title ?? "The module list could not be loaded.");
            this.moduleFiles = body;
        },
        openDeleteDialog(moduleFile) {
            this.modulePendingDeletion = moduleFile;
            this.$refs.deleteDialog.showModal();
        },
        async deleteModule() {
            this.isDeleting = true;
            this.error = "";
            try {
                const response = await fetch(
                    `/api/dashboard/module-files/${encodeURIComponent(this.modulePendingDeletion)}`,
                    { method: "DELETE" });
                if (!response.ok) throw new Error("The module could not be deleted. Refresh the page and try again.");
                this.$refs.deleteDialog.close();
                await this.loadDashboard(true);
            } catch (error) {
                this.error = error.message;
            } finally {
                this.isDeleting = false;
            }
        }
    }
}).mount("#dashboard-app");
