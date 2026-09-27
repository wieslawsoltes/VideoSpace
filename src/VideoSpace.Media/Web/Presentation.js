/* Optional monitor overlays and visibility; media/GPU modules remain usable independently. */
(function (g) {
  const hiddenViews = () => {
    for (const [id, visible] of Object.entries(g.VideoSpaceVisibility || {})) { const canvas = document.getElementById('videospace-' + id); if (canvas && !visible) canvas.style.display = 'none'; }
    requestAnimationFrame(hiddenViews);
  };
  requestAnimationFrame(hiddenViews);
  // Visibility is enforced by the compositor tick as well; this handles the first asynchronous canvas creation.
})(globalThis);
