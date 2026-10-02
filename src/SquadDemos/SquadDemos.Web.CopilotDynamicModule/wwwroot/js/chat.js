const { createApp } = Vue;

createApp({
    data() {
        return { sessions: [], selectedSession: null, selectedSessionId: null, message: "", isSending: false, error: "" };
    },
    async mounted() {
        await this.loadSessions();
    },
    methods: {
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
                this.selectedSession = await this.readJson(response);
                this.selectedSessionId = sessionId;
            } catch (error) {
                this.error = error.message;
            }
        },
        startNewChat() {
            this.selectedSession = null;
            this.selectedSessionId = null;
            this.message = "";
            this.error = "";
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
        }
    }
}).mount("#chat-app");
