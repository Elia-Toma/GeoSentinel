export async function apiFetch(url, options) {
    const res = await fetch(url, options);
    if (!res.ok) {
        const txt = await res.text();
        throw new Error(txt || res.statusText);
    }
    return res.json();
}
