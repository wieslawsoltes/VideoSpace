/* MIT. Explicit JSON-only .NET interop. UI exports use the offline frame/sample pipeline. */
(function (g) {
  'use strict';
  g.VideoSpaceBridge = {
    call(operation, payload) {
      const value = JSON.parse(payload || 'null'), media = g.VideoSpaceMedia;
      switch (operation) {
        case 'init': media.init(); break;
        case 'drain': return media.drain();
        case 'pickMedia': media.pickMedia(); break;
        case 'pickProject': media.pickProject(); break;
        case 'present': media.present(value); break;
        case 'hide': media.hide(value); break;
        case 'unlockAudio': media.unlockAudio(); break;
        case 'autosave': media.autosave(value); break;
        case 'downloadText': media.downloadText(value.text, value.name); break;
        case 'downloadBase64': media.downloadBase64(value.bytes, value.name); break;
        case 'exportVideo': g.VideoSpaceOffline.run(value.project, value.height); break;
        case 'exportStill': media.exportStill(); break;
        case 'cancelExport': g.VideoSpaceOffline.cancel(); media.cancelExport(); break;
        case 'setProject': media.setProject(value); break;
        case 'diagnostics': g.videoSpaceState = value; break;
        case 'controls': g.videoSpaceControls = value; break;
        case 'controlInvalidated': if (g.videoSpaceControls) delete g.videoSpaceControls[value]; break;
        case 'meter': return JSON.stringify(media?.diagnostics?.meter || [0, 0]);
        case 'visibility': document.documentElement.toggleAttribute('data-videospace-' + value.id + '-hidden', !value.visible); break;
        case 'dispose': g.VideoSpaceOffline.cancel(); media.dispose(); break;
        default: throw new Error('Unsupported VideoSpace bridge operation: ' + operation);
      }
      return 'ok';
    }
  };
})(globalThis);
