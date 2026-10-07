window.apiEndpoints = Object.freeze({
    copilot: Object.freeze({
        authentication: "/api/copilot/authentication",
        models: "/api/copilot/models",
        selectedModel: "/api/copilot/selected-model",
        beginDeviceFlow: "/api/copilot/device-flow/begin",
        completeDeviceFlow: "/api/copilot/device-flow/complete",
        signOut: "/api/copilot/sign-out"
    }),
    skills: Object.freeze({
        uploaded: "/api/skills/uploaded",
        search: "/api/skills/search",
        upload: "/api/skills/upload",
        install: "/api/skills/install",
        preview: "/api/skills/preview",
        remove: name => `/api/skills/uploaded/${encodeURIComponent(name)}`
    }),
    chat: Object.freeze({
        sessions: "/api/chat/sessions",
        session: id => `/api/chat/sessions/${id}`,
        messages: "/api/chat/messages"
    }),
    dashboard: Object.freeze({
        modules: "/api/dashboard/modules",
        refreshModules: "/api/dashboard/modules/refresh",
        moduleFiles: "/api/dashboard/module-files",
        moduleFile: name => `/api/dashboard/module-files/${encodeURIComponent(name)}`
    })
});
