const { createApp } = Vue;
createApp({
    data: () => ({ uploaded: { items: [], totalCount: 0 }, results: null, term: "", previewSkill: null, status: "", error: "", uploading: false, notificationTimer: null }),
    async mounted() { await this.load(); this.notify("Skills library ready."); },
    methods: {
        notify(message, isError = false) {
            clearTimeout(this.notificationTimer);
            this.status = isError ? "" : message;
            this.error = isError ? message : "";
            this.notificationTimer = setTimeout(() => {
                this.status = "";
                this.error = "";
            }, 5000);
        },
        async read(response) { const body = response.status === 204 ? null : await response.json(); if (!response.ok) throw new Error(body.detail ?? body ?? "Request failed."); return body; },
        async load() { try { this.uploaded = await this.read(await fetch(`${apiEndpoints.skills.uploaded}?page=1`)); } catch (error) { this.notify(error.message, true); } },
        async search() { try { this.results = await this.read(await fetch(`${apiEndpoints.skills.search}?term=${encodeURIComponent(this.term)}&page=1`)); this.notify(`Found ${this.results.totalCount} skills.`); } catch (error) { this.notify(error.message, true); } },
        async upload() { const [file] = this.$refs.file.files; if (!file) return; this.uploading = true; try { const form = new FormData(); form.append("skillFile", file); const skill = await this.read(await fetch(apiEndpoints.skills.upload, { method: "POST", body: form })); this.notify(`Uploaded ${skill.name}.`); this.$refs.file.value = ""; await this.load(); } catch (error) { this.notify(error.message, true); } finally { this.uploading = false; } },
        async install(name) { try { this.notify(await this.read(await fetch(`${apiEndpoints.skills.install}/${encodeURIComponent(name)}`, { method: "POST" }))); await this.load(); } catch (error) { this.notify(error.message, true); } },
        async remove(name) { if (!confirm(`Delete ${name}?`)) return; try { await this.read(await fetch(apiEndpoints.skills.remove(name), { method: "DELETE" })); this.notify(`Deleted ${name}.`); await this.load(); } catch (error) { this.notify(error.message, true); } },
        async preview(name) { try { this.previewSkill = await this.read(await fetch(`${apiEndpoints.skills.preview}/${encodeURIComponent(name)}`)); this.$refs.dialog.showModal(); } catch (error) { this.notify(error.message, true); } }
    }
}).mount("#skills-uploader");
