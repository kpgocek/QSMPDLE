window.qsmpdle = (() => {

    async function share(text) {

        // Native mobile / desktop share sheet
        if (navigator.share) {
            try {
                await navigator.share({
                    title: "QSMPDLE",
                    text: text,
                    url: "https://qsmpdle.com"
                });

                return {
                    success: true,
                    method: "share"
                };
            }
            catch {
                // user cancelled
            }
        }

        // Clipboard fallback
        try {
            await navigator.clipboard.writeText(text);

            return {
                success: true,
                method: "clipboard"
            };
        }
        catch {
            return {
                success: false,
                method: "none"
            };
        }
    }

    function openTwitter(text) {
        const url =
            "https://twitter.com/intent/tweet?text=" +
            encodeURIComponent(text);

        window.open(url, "_blank");
    }

    function openBluesky(text) {
        const url =
            "https://bsky.app/intent/compose?text=" +
            encodeURIComponent(text);

        window.open(url, "_blank");
    }

    function openReddit(text) {
        navigator.clipboard.writeText(text);

        window.open(
            "https://www.reddit.com/submit",
            "_blank");
    }

    function openDiscord(text) {
        navigator.clipboard.writeText(text);

        window.open(
            "https://discord.com/channels/@me",
            "_blank");
    }

    return {
        share,
        openTwitter,
        openBluesky,
        openReddit,
        openDiscord
    };
})();
