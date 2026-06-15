window.scrollToTop = () => {
    const element = document.getElementById("game-top");

    if (!element)
        return;

    const offset = 16;

    const y =
        element.getBoundingClientRect().top +
        window.pageYOffset -
        offset;

    window.scrollTo({
        top: y,
        behavior: "smooth"
    });
};
