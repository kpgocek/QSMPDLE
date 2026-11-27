// One interop round trip for every key; the storage library only offers key(i), one per key.
// Only what follows the prefix is sent: full keys of a few years of games exceed the connection's message limit.
window.storageKeys = prefix => Object.keys(localStorage).filter(key => key.startsWith(prefix)).map(key => key.slice(prefix.length));
