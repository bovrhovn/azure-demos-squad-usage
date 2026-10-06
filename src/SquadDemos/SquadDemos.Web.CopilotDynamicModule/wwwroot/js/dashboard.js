const { createApp } = Vue;

createApp({
    data() {
        return {
            modules: [],
            moduleFiles: [],
            modulePendingDeletion: "",
            moduleRequest: "",
            isCreating: false,
            isUploading: false,
            isDeleting: false,
            isLoading: false,
            error: "",
            hubConnection: null,
            generationProgress: []
        };
    },
    async mounted() {
        await this.startHubConnection();
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
        async startHubConnection() {
            if (!window.signalR) {
                this.error = "Live module-generation updates are unavailable because SignalR could not be loaded.";
                return;
            }

            const connection = new signalR.HubConnectionBuilder()
                .withUrl("/hubs/dashboard")
                .withAutomaticReconnect()
                .build();

            connection.on("ModuleGenerationProgress", progress => {
                const progressIndex = this.generationProgress.findIndex(item => item.stage === progress.stage);
                if (progressIndex === -1) {
                    this.generationProgress.push(progress);
                } else {
                    this.generationProgress.splice(progressIndex, 1, progress);
                }
            });
            connection.onreconnected(async () => await this.joinDashboard());

            try {
                await connection.start();
                this.hubConnection = connection;
                await this.joinDashboard();
            } catch (error) {
                this.error = "Live module-generation updates could not be connected.";
            }
        },
        async joinDashboard() {
            if (!this.hubConnection) return;

            try {
                await this.hubConnection.invoke("JoinDashboard");
            } catch (error) {
                this.error = "Live module-generation updates could not be connected.";
            }
        },
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
        async createModule() {
            this.isCreating = true;
            this.error = "";
            this.generationProgress = [];
            try {
                const response = await fetch("/api/dashboard/modules", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ prompt: this.moduleRequest })
                });
                const body = await response.json();
                if (!response.ok) throw new Error(body.detail ?? body.title ?? body ?? "The module could not be created.");
                this.moduleRequest = "";
                await this.loadDashboard(true);
            } catch (error) {
                this.error = error.message;
            } finally {
                this.isCreating = false;
            }
        },
        async uploadModule(event) {
            const [moduleFile] = event.target.files;
            if (!moduleFile) return;

            this.isUploading = true;
            this.error = "";
            try {
                const formData = new FormData();
                formData.append("moduleFile", moduleFile);
                const response = await fetch("/api/dashboard/module-files", {
                    method: "POST",
                    body: formData
                });
                const body = await response.json();
                if (!response.ok) throw new Error(body.detail ?? body.title ?? body ?? "The module could not be uploaded.");
                await this.loadDashboard(true);
            } catch (error) {
                this.error = error.message;
            } finally {
                event.target.value = "";
                this.isUploading = false;
            }
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
