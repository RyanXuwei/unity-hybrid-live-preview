// Hybrid Live Preview: lets the player know it runs in a page made by Build WebGL (see src/hlp/webgl.js).
mergeInto(LibraryManager.library, {
  HlpIsHybridPage: function () {
    return window.__hlpPage === true;
  }
});
