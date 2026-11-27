window.qsmpdleShare = {
    async share(text, url) {

        if (navigator.share) {
            try {
                await navigator.share({
                    text,
                    url
                });

                return true;
            } catch (error) {
                // Cancelling the sheet counts as handled; anything else (e.g. NotAllowedError once the
                // click's activation lapsed during the server round trip) falls back to copying.
                return error?.name === 'AbortError';
            }
        }

        return false;
    },

    async copy(text) {
        try {
            if (navigator.clipboard?.writeText) {
                try {
                    await navigator.clipboard.writeText(text);
                    return true;
                } catch {
                    // Denied or unfocused clipboard access still has the legacy path below.
                }
            }

            const textarea = document.createElement('textarea');
            textarea.value = text;
            textarea.setAttribute('readonly', '');
            textarea.style.position = 'fixed';
            textarea.style.opacity = '0';
            document.body.appendChild(textarea);
            textarea.select();

            try {
                return document.execCommand('copy');
            } finally {
                textarea.remove();
            }
        } catch {
            return false;
        }
    }
};
