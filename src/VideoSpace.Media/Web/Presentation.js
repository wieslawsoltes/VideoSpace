/* Visibility of DOM-backed video surfaces follows the hosting Uno panels. */
(function () {
  const style = document.createElement('style');
  style.textContent = 'html[data-videospace-source-hidden] #videospace-source,html[data-videospace-program-hidden] #videospace-program{display:none!important}html,body{background:#18191b!important}';
  document.head.append(style);
})();
