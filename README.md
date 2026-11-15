# Wynnyng — partida de teste contra a CPU

Partida original 3D para PC: **Aurora FC × Horizonte AC**, dois clubes fictícios, com 11 jogadores de cada lado. Você controla o time azul; o adversário e seus companheiros usam IA. A versão padrão abre a partida, com câmera próxima da ação, em vez da bancada de física.

## Jogar no Windows

1. Feche o jogo antigo e as janelas do Unity.
2. Execute novamente [GERAR_E_JOGAR.exe](https://github.com/YokiPJ/Wynnyng-six-seven/raw/refs/heads/codex/phase-0-1a/GERAR_E_JOGAR.exe).
3. Aguarde o download da revisão, preparação, testes e build. O jogo abre após sucesso.

O preparador já baixado também consulta a versão atual automaticamente. O atalho **Jogar Wynnyng** abre o player daquela revisão; use o preparador para atualizar. O clone original é preservado.

**O arquivo entregue é o preparador.** Ele compila o jogo no seu PC usando Unity **6000.0.62f1**, instalado/ativado, com **Windows Build Support (Mono)**. O ambiente de desenvolvimento não tem Editor/licença Unity e não gerou o player aqui. [Detalhes do Windows e logs](docs/WINDOWS.md).

## O que está implementado

- Dois times 11×11, dois goleiros, modelos humanos de blockout, uniformes de cores diferentes, números e indicação de seleção.
- Jogador com aceleração, giro limitado, sprint e stamina; troca manual e automática.
- Bola física independente, drible por toques, passe assistido, profundidade, lançamento e chute carregado com curva.
- CPU com formação 4-4-2, perseguição por interceptação, apoio, pressão, passes, finalização, bote e defesa/rebatida do goleiro.
- Placar, relógio de 90 minutos comprimidos em cinco minutos reais, dois tempos, troca de lados, intervalo e fim de jogo.
- Gol contado uma vez; bola fora tem **reposição simplificada de treino**. Não representa regras completas de laterais/escanteios.
- Menu de pausa, nova partida, CPU × CPU para observar a IA, dois jogadores locais com dois controles, remapeamento e ajustes em runtime.

Os clubes e jogadores são fictícios; não há escudos, elenco ou marcas oficiais. Os jogadores são identificados por números, sem inventar dados reais.

## Controles

| Ação | Teclado | Gamepad |
|---|---|---|
| Mover / apontar | WASD ou setas | Analógico esquerdo |
| Chutar, segurar para força / bote sem posse | J | Sul |
| Passe / bote sem posse | K | Oeste |
| Passe em profundidade | I | Norte |
| Lançamento / bote sem posse | L | Leste |
| Sprint | Shift esquerdo | Gatilho direito |
| Trocar jogador | Tab | Ombro esquerdo |
| Pausar / menu | Esc | Start |

O movimento acompanha a câmera. Sem gamepad, o jogador 1 usa o teclado; ao conectar um pad, ele passa a controlar o jogador 1. No menu, **Remapear controles** salva os overrides neste PC. Dois jogadores locais exigem dois controles; o teclado é fallback apenas do jogador 1. Se o segundo controle desconectar, o adversário volta à CPU.

Para finalizar, segure e solte **J / Sul**. Perto do gol, soltar sem direcional busca um canto livre; inclinar para um lado escolhe o canto com assistência dentro da boca do gol. A corrida desacelera enquanto você carrega o chute. O goleiro tem reação e alcance limitados, mas continua defendendo bolas direcionadas ao corpo.

## Ajustar a sensação

Abra **Esc / Start → Ajustar a sensação**. Os sliders alteram corrida, sprint, força máxima de passe/chute e raio de pressão da CPU. As mudanças afetam somente a cópia da partida atual e são preservadas ao escolher Nova partida; reiniciar o aplicativo carrega o SO original.

| Parâmetro | Inicial | Efeito |
|---|---|---|
| Corrida / sprint | 5,2 / 9 m/s | Ritmo e alcance do jogador |
| Aceleração / frenagem | 16 / 20 m/s² | Peso e resposta ao direcional |
| Passe mínimo / máximo | 12 / 22 m/s | Firmeza do passe assistido |
| Chute mínimo / máximo | 18 / 32 m/s | Força ao soltar J/Sul |
| Carga máxima | 0,95 s | Tempo da barra de chute |
| Decisões de formação | 0,1 s | Frequência tática da CPU |
| Raio de pressão | 3,2 m | Quando a CPU considera passar sob pressão |
| Duração | 300 s | Tempo de bola em jogo; pausas/reinícios acrescentam tempo real |

Todos os valores ficam em `Assets/Wynnyng/Resources/Config/Football.asset`, nos campos de `FootballConfig.Gameplay`, além dos SOs Ball/Pitch/Simulation/Presentation. Formação é um array de 11 posições normalizadas; velocidade e aparência são dados editáveis, não código espalhado pelo loop.

## Abrir pelo Editor

1. Abra a pasta com `Assets`, `Packages` e `ProjectSettings` no Unity 6000.0.62f1.
2. Aguarde a importação dos pacotes.
3. Clique em **Wynnyng → Preparar e jogar partida**. O comando prepara TMP/URP e abre a cena nova.
4. Para abrir sem iniciar Play, use **Wynnyng → Abrir partida**.
5. Para diagnóstico, **Wynnyng → Abrir laboratorio Fase 1A** mantém a bancada antiga.
6. Para gerar pelo Editor, use **Wynnyng → Gerar EXE Windows**; o script externo também roda os testes.

A cena padrão é `Assets/Wynnyng/Scenes/FootballMatch.unity`. Fora do Play, contém somente o bootstrap; campo e jogadores são gerados ao iniciar. Não abrir a cena da partida e a do laboratório simultaneamente.

Unity 6, URP 17.0.4, Input System 1.11.2, Cinemachine 3.1.3 (`Unity.Cinemachine`), uGUI 2.0.0 com TMP e Test Framework 1.4.5. Não instalar o pacote antigo independente TextMeshPro.

## Arquitetura e arquivos completos

O código completo está no repositório; nenhum trecho precisa ser colado por cima. [Árvore completa](docs/ARQUIVOS.md).

```text
Assets/Wynnyng/
  Core/Football*.cs           Simulação tática, movimento, ações, relógio e estatísticas sem Unity
  Core/PlayerCommandBuffer.cs Bordas de entrada consumidas uma vez por passo fixo
  Core/IFootballControls.cs   Contrato entre apresentação e runtime
  Configuration/FootballConfig.cs
  Resources/Config/Football.asset
  Runtime/FootballBootstrap.cs  Ponte com bola e 22 cápsulas kinematic/interpoladas
  Presentation/FootballCameraRig.cs
  Presentation/FootballHud.cs
  Presentation/FootballPlayerVisuals.cs
  Scenes/FootballMatch.unity
  Tests/EditMode/Football*Tests.cs
  Tests/EditMode/PlayerCommandBufferTests.cs
  Tests/PlayMode/FootballMatchAcceptanceTests.cs
```

O núcleo usa passo de 60 Hz e RNG injetado. PhysX mantém a bola; a simulação recebe posição/velocidade e devolve toques, chutes ou reposições. Entrada é amostrada no Update e acumulada para não perder nem repetir botões no FixedUpdate. A física dos jogadores é kinematic, com separação calculada pelo núcleo e render interpolado. Materiais e malhas procedurais são criados uma vez e liberados com o mundo.

## Testes e checklist

Executados fora do Unity: **80 testes .NET**, incluindo 11 regressões de finalização com goleiro ativo, CPU × CPU durante 600 segundos simulados e duas verificações de 60.000 passos sem alocação gerenciada no núcleo. Não são medições de FPS nem testes de PhysX. As verificações do preparador são separadas do jogo. [Resultados e limites](docs/VALIDACAO.md).

Preparados para o Unity: **103 casos EditMode e 21 PlayMode**, incluindo um chute com bola e colisões PhysX contra o goleiro. O único teste de pixels antigo é ignorado em batch; qualquer outro ignore/falha bloqueia o preparador.

Checklist no player:

- Ver 22 jogadores e dois uniformes; começar com você contra a CPU.
- Mover, correr, passar, carregar/soltar chute e trocar jogador.
- Observar o adversário perseguir, passar, finalizar e o goleiro rebater.
- Finalizar de 6, 12 e 18 m, parado e mirando para os cantos; conferir defesa de chute fraco central, placar/reinício e reposição de treino quando a bola sai.
- Pausar e confirmar que bola e relógio param; ajustar sliders e continuar.
- Escolher CPU × CPU e Nova partida; testar dois controles e desconectar o segundo.
- Completar dois tempos e reiniciar; fechar/reabrir o player.
- Medir FPS/GC no Windows após aquecimento e validar o teste de pixels no Editor gráfico.

## Limites conhecidos

Esta é uma partida de teste jogável com arte procedural, ainda em desenvolvimento. IA e goleiro são simplificados e precisam de balanceamento no player; as estatísticas do teste sintético não provam bom ritmo numa partida real. Não há regras completas, arbitragem, impedimento, cartões, substituições, cabeceios/voleios, animação Humanoid, áudio, replays, Brasileirão, save/carreira ou modo edição. Esses sistemas não estão sendo apresentados como concluídos.

O teste .NET usa um integrador de bola simplificado; não substitui a execução no Unity, colisões reais, gamepads físicos ou 60 FPS na GTX 1060. [HANDOFF atualizado](HANDOFF.md) contém os contratos e pendências para continuidade.
