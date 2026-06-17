window.qsmpdleShare = {
    async share(text, url) {

        if (navigator.share) {
            try {
                await navigator.share({
                    text,
                    url
                });

                return true;
            }
            catch {
                return true;
            }
        }

        return false;
    }
};
