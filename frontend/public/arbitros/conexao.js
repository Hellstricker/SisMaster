/*
 * Conexão em tempo real (SignalR) das telas da mesa, pensada para rede ruim na quadra:
 *  - reconecta SEM desistir (o padrão do SignalR para depois de ~40 s e a tela ficaria morta até recarregar);
 *  - percebe a queda rápido (batimento de 5 s, tolerância de 15 s) e quando o aparelho fica sem rede (evento "offline");
 *  - ao reconectar entra de novo na sala da partida e o servidor reenvia o estado atual (EstadoAtual);
 *  - se a página abrir sem rede, continua tentando até conseguir;
 *  - ação sem conexão é recusada na hora (nunca finge que foi enviada).
 * Uso: var con = SisMasterConexao.criar({ partidaId, entrar: 'EntrarComoMesarioTempo', aoMudar: function (ok) {...}, registrar: function (connection) {...} });
 */
(function () {
  function criar(opcoes) {
    var conectado = false;
    var tentando = false;

    var connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/sumula?partidaId=' + encodeURIComponent(opcoes.partidaId), {
        skipNegotiation: true,
        transport: signalR.HttpTransportType.WebSockets,
        accessTokenFactory: function () { return window.SisMasterAuth ? window.SisMasterAuth.token() : ''; }
      })
      // Nunca devolve null: tenta de novo a cada 1 a 5 segundos, para sempre.
      .withAutomaticReconnect({ nextRetryDelayInMilliseconds: function (ctx) { return Math.min(1000 * (ctx.previousRetryCount + 1), 5000); } })
      .withKeepAliveInterval(5000)
      .withServerTimeout(15000)
      .build();

    if (opcoes.registrar) opcoes.registrar(connection);

    function mudou(ok) {
      if (conectado === ok) return;
      conectado = ok;
      if (opcoes.aoMudar) opcoes.aoMudar(ok);
    }

    function entrarNaSala() {
      return opcoes.entrar ? connection.invoke(opcoes.entrar, opcoes.partidaId) : Promise.resolve();
    }

    function iniciar() {
      if (tentando) return;
      tentando = true;
      connection.start()
        .then(entrarNaSala)
        .then(function () { tentando = false; mudou(true); })
        .catch(function () { tentando = false; mudou(false); setTimeout(iniciar, 3000); });
    }

    connection.onreconnecting(function () { mudou(false); });
    connection.onreconnected(function () {
      // O servidor já reenvia o estado ao reconectar; entrar de novo na sala garante o grupo da mesa.
      entrarNaSala().then(function () { mudou(true); }).catch(function () { mudou(true); });
    });
    // Só chega aqui se a conexão for encerrada de vez: recomeça.
    connection.onclose(function () { mudou(false); setTimeout(iniciar, 3000); });

    // O aparelho perdeu a rede: avisa já, sem esperar o tempo limite do SignalR.
    window.addEventListener('offline', function () { mudou(false); });
    window.addEventListener('online', function () {
      if (connection.state === signalR.HubConnectionState.Connected) mudou(true);
    });

    iniciar();

    return {
      connection: connection,
      conectado: function () { return conectado && connection.state === signalR.HubConnectionState.Connected; },
      /** Chama um método do hub; sem conexão, falha na hora com Error('SEM_CONEXAO'). */
      invocar: function (metodo) {
        var args = Array.prototype.slice.call(arguments, 1);
        if (!this.conectado()) return Promise.reject(new Error('SEM_CONEXAO'));
        return connection.invoke.apply(connection, [metodo].concat(args));
      }
    };
  }

  window.SisMasterConexao = { criar: criar };
})();
