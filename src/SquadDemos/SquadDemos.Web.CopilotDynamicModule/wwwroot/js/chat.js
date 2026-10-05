const { createApp } = Vue;

createApp({
    data() {
        return {
            sessions: [],
            selectedSession: null,
            selectedSessionId: null,
            message: "",
            isSending: false,
            error: "",
            copiedMessageId: null,
            hubConnection: null
        };
    },
    async mounted() {
        await this.startHubConnection();
        await this.loadSessions();
    },
    methods: {
        async startHubConnection() {
            if (!window.signalR) {
                this.error = "Live chat updates are unavailable because SignalR could not be loaded.";
                return;
            }

            const connection = new signalR.HubConnectionBuilder()
                .withUrl("/hubs/chat")
                .withAutomaticReconnect()
                .build();

            connection.on("SessionUpdated", session => {
                if (this.selectedSessionId === session.id) {
                    this.selectedSession = session;
                }

                this.sessions = [{ id: session.id, title: session.title, updatedAt: session.updatedAt },
                    ...this.sessions.filter(item => item.id !== session.id)];
            });
            connection.onreconnected(async () => await this.joinSelectedSession());

            try {
                await connection.start();
                this.hubConnection = connection;
            } catch (error) {
                this.error = "Live chat updates could not be connected.";
            }
        },
        async loadSessions() {
            this.error = "";
            try {
                const response = await fetch("/api/chat/sessions");
                this.sessions = await this.readJson(response);
                if (this.sessions.length > 0) await this.selectSession(this.sessions[0].id);
            } catch (error) {
                this.error = error.message;
            }
        },
        async selectSession(sessionId) {
            this.error = "";
            try {
                const response = await fetch(`/api/chat/sessions/${sessionId}`);
                const session = await this.readJson(response);
                await this.leaveSession(this.selectedSessionId);
                this.selectedSession = session;
                this.selectedSessionId = sessionId;
                await this.joinSelectedSession();
            } catch (error) {
                this.error = error.message;
            }
        },
        startNewChat() {
            this.leaveSelectedSession();
            this.selectedSession = null;
            this.selectedSessionId = null;
            this.message = "";
            this.error = "";
            this.copiedMessageId = null;
        },
        async joinSelectedSession() {
            if (!this.hubConnection || !this.selectedSessionId) return;

            try {
                await this.hubConnection.invoke("JoinSession", this.selectedSessionId);
            } catch (error) {
                this.error = "Live chat updates could not join the selected conversation.";
            }
        },
        async leaveSelectedSession() {
            await this.leaveSession(this.selectedSessionId);
        },
        async leaveSession(sessionId) {
            if (!this.hubConnection || !sessionId) return;

            try {
                await this.hubConnection.invoke("LeaveSession", sessionId);
            } catch (error) {
                this.error = "Live chat updates could not leave the selected conversation.";
            }
        },
        async sendMessage() {
            if (!this.message || this.isSending) return;
            this.isSending = true;
            this.error = "";
            try {
                const response = await fetch("/api/chat/messages", {
                    method: "POST",
                    headers: { "Content-Type": "application/json" },
                    body: JSON.stringify({ sessionId: this.selectedSessionId, message: this.message })
                });
                const session = await this.readJson(response);
                this.selectedSession = session;
                this.selectedSessionId = session.id;
                this.message = "";
                this.sessions = [{ id: session.id, title: session.title, updatedAt: session.updatedAt },
                    ...this.sessions.filter(item => item.id !== session.id)];
            } catch (error) {
                this.error = error.message;
            } finally {
                this.isSending = false;
            }
        },
        async readJson(response) {
            const body = await response.json();
            if (!response.ok) throw new Error(body.detail ?? body.title ?? "The request failed.");
            return body;
        },
        formatDate(value) {
            return new Intl.DateTimeFormat(undefined, { dateStyle: "medium" }).format(new Date(value));
        },
        async copyMessage(message) {
            try {
                await navigator.clipboard.writeText(message.content);
                this.copiedMessageId = message.id;
            } catch (error) {
                this.error = "Unable to copy the agent response.";
            }
        },
        formatTokenUsage(tokenUsage) {
            if (!tokenUsage) return "Token usage unavailable.";

            const usage = [
                ["Input", tokenUsage.inputTokens],
                ["Output", tokenUsage.outputTokens],
                ["Reasoning", tokenUsage.reasoningTokens]
            ].filter(([, count]) => Number.isInteger(count));

            return usage.length === 0
                ? "Token usage unavailable."
                : `Token usage: ${usage.map(([name, count]) => `${name} ${count.toLocaleString()}`).join(" · ")}`;
        }
    }
}).mount("#chat-app");
