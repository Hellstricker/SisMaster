import { BrowserRouter, Route, Routes } from 'react-router-dom';
import { ThemeContext } from './contexts/ThemeContext';
import { useTheme } from './hooks/useTheme';
import AppLayout from './components/Layout/AppLayout';
import ListaAssociacoes from './pages/Associacoes/ListaAssociacoes';
import DetalheAssociacao from './pages/Associacoes/DetalheAssociacao';
import DetalheCampeonato from './pages/Campeonatos/DetalheCampeonato';
import DetalheTemporada from './pages/Temporadas/DetalheTemporada';
import CategoriasAssociacao from './pages/Categorias/CategoriasAssociacao';
import InscricoesTemporada from './pages/Inscricoes/InscricoesTemporada';
import EquipesTemporada from './pages/Equipes/EquipesTemporada';
import FasesTemporada from './pages/Fases/FasesTemporada';
import DetalheFase from './pages/Fases/DetalheFase';
import LocaisAssociacao from './pages/Locais/LocaisAssociacao';
import ClassificacaoFase from './pages/Fases/ClassificacaoFase';
import JogosTemporada from './pages/Jogos/JogosTemporada';
import DetalheJogo from './pages/Jogos/DetalheJogo';
import PrepararSumula from './pages/Sumula/PrepararSumula';
import ImportarFiba from './pages/Sumula/ImportarFiba';
import JogosDoDia from './pages/Arbitros/JogosDoDia';
import { AREA_ARBITROS } from './utils/area';

export default function App() {
  const { theme, toggle } = useTheme();

  return (
    <ThemeContext.Provider value={{ theme, toggle }}>
      <BrowserRouter basename={AREA_ARBITROS ? '/arbitros' : '/admin'}>
        {AREA_ARBITROS ? (
          // Área do árbitro: só os jogos agendados e a súmula (nada de inscrições, financeiro ou cadastros).
          <Routes>
            <Route element={<AppLayout />}>
              <Route path="/" element={<JogosDoDia />} />
              <Route path="/jogos/:id/sumula" element={<PrepararSumula />} />
              <Route path="*" element={<JogosDoDia />} />
            </Route>
          </Routes>
        ) : (
        <Routes>
          <Route element={<AppLayout />}>
            <Route path="/" element={<ListaAssociacoes />} />
            <Route path="/associacoes" element={<ListaAssociacoes />} />
            <Route path="/associacoes/:id" element={<DetalheAssociacao />} />
            <Route path="/associacoes/:id/categorias" element={<CategoriasAssociacao />} />
            <Route path="/associacoes/:id/locais" element={<LocaisAssociacao />} />
            <Route path="/campeonatos/:id" element={<DetalheCampeonato />} />
            <Route path="/temporadas/:id" element={<DetalheTemporada />} />
            <Route path="/temporadas/:id/inscricoes" element={<InscricoesTemporada />} />
            <Route path="/temporadas/:id/equipes" element={<EquipesTemporada />} />
            <Route path="/temporadas/:id/fases" element={<FasesTemporada />} />
            <Route path="/fases/:id" element={<DetalheFase />} />
            <Route path="/fases/:id/classificacao" element={<ClassificacaoFase />} />
            <Route path="/temporadas/:id/jogos" element={<JogosTemporada />} />
            <Route path="/jogos/:id" element={<DetalheJogo />} />
            <Route path="/jogos/:id/sumula" element={<PrepararSumula />} />
            <Route path="/jogos/:id/sumula/importar-fiba" element={<ImportarFiba />} />
          </Route>
        </Routes>
        )}
      </BrowserRouter>
    </ThemeContext.Provider>
  );
}
