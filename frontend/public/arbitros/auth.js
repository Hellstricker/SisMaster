/*
 * Sessão para as telas estáticas da mesa (mesa, mesário de tempo, mesário de estatísticas).
 * O login acontece no app React (/arbitros/entrar), que guarda a sessão no localStorage; estas páginas só a leem.
 * O placar ao vivo é público e não usa este arquivo.
 */
(function () {
  var CHAVE = 'sismaster.sessao';

  function sessao() {
    try {
      var s = JSON.parse(localStorage.getItem(CHAVE) || 'null');
      return s && Date.now() < s.expiraEm ? s : null;
    } catch (e) { return null; }
  }

  function irParaOLogin() {
    localStorage.removeItem(CHAVE);
    location.replace('/arbitros/entrar?voltar=' + encodeURIComponent(location.pathname + location.search));
  }

  window.SisMasterAuth = {
    sessao: sessao,
    token: function () { var s = sessao(); return s ? s.accessToken : ''; },

    /** Sem sessão válida, leva ao login e devolve false. */
    exigirLogin: function () {
      if (sessao()) return true;
      irParaOLogin();
      return false;
    },

    /** fetch que leva o token; um 401 (sessão vencida) leva ao login. */
    fetch: function (url, init) {
      init = init || {};
      var cabecalhos = new Headers(init.headers || {});
      var t = this.token();
      if (t) cabecalhos.set('Authorization', 'Bearer ' + t);
      init.headers = cabecalhos;
      return window.fetch(url, init).then(function (r) {
        if (r.status === 401) irParaOLogin();
        return r;
      });
    }
  };

  // Mantém a sessão viva enquanto o árbitro opera a mesa (o token de árbitro dura 8 h; renova ao passar da metade).
  var ultima = Date.now();
  ['pointerdown', 'keydown', 'touchstart'].forEach(function (e) { window.addEventListener(e, function () { ultima = Date.now(); }, { passive: true }); });
  setInterval(function () {
    var s = sessao();
    if (!s) return;
    var duracao = s.expiraEm - s.emitidoEm, restante = s.expiraEm - Date.now();
    if (restante < duracao * 0.6 && Date.now() - ultima < 5 * 60000) {
      window.SisMasterAuth.fetch('/api/identidade/renovar', { method: 'POST' }).then(function (r) { return r.ok ? r.json() : null; }).then(function (nova) {
        if (!nova) return;
        var agora = Date.now();
        localStorage.setItem(CHAVE, JSON.stringify({ accessToken: nova.accessToken, emitidoEm: agora, expiraEm: agora + nova.expiresIn * 1000, usuario: nova.userToken }));
      }).catch(function () { /* tenta no próximo ciclo */ });
    }
  }, 30000);
})();
