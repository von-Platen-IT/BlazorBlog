// culture.js — Helper for Blazor Server culture switching via cookie
window.blazorCulture = {
    get: () => {
        const match = document.cookie.match(new RegExp('(^| )\\.AspNetCore\\.Culture=([^;]+)'));
        if (!match) return null;
        const value = decodeURIComponent(match[2]);
        const c = value.match(/c=([^|]+)/);
        return c ? c[1] : null;
    },
    set: (culture) => {
        const expiry = new Date(Date.now() + 365 * 86400000).toUTCString();
        document.cookie =
            `.AspNetCore.Culture=c=${culture}|uic=${culture};expires=${expiry};path=/;samesite=strict`;
    }
};
