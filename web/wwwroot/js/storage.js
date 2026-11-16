// One interop round trip for every key; the storage library only offers key(i), one per key.
window.storageKeys = () => Object.keys(localStorage);
