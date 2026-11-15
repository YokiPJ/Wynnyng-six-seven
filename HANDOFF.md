# HANDOFF — Wynnyng: partida de teste 11×11

## Autorização atual

Usuário iniciante, português, Unity 6/URP e PC/gamepad. Pedido inicial autorizava laboratório Fases 0/1A e exigia aguardar “próxima” para avançar. **Pedido posterior explícito substituiu essa restrição para esta entrega:** “coloque CPU pra jogar contra e dois times pra teste, arrume todo o jogo”. A entrega atual implementa uma partida de teste com elementos de 1B/2 e HUD de apoio. Não afirmar que todo o roteiro original, regras completas ou Brasileirão foram concluídos.

Repositório: https://github.com/YokiPJ/Wynnyng-six-seven . Branch de entrega `codex/phase-0-1a`, também consultada pelo preparador Windows. A cena padrão do player é `Assets/Wynnyng/Scenes/FootballMatch.unity`; o laboratório `Phase01A.unity` é preservado para diagnóstico.

## Versões e ambiente

Unity **6000.0.62f1**, revisão f99f05b3e950; URP 17.0.4; Input System 1.11.2; Cinemachine 3.1.3 namespace Unity.Cinemachine; uGUI 2.0.0/TMP; Test Framework 1.4.5. Fixed60 Hz; novo Input System (`activeInputHandler:1`). Sem APIs Input legadas; Rigidbody usa linearVelocity/linearDamping.

Ambiente de desenvolvimento Linux sem Editor Unity/licença. SDK .NET 8.0.428 e PowerShell 7.4.6 estão disponíveis. **Nenhum player Unity foi gerado/executado aqui.** O preparador .NET Framework 4.8 foi compilado separadamente e gera o jogo no PC Windows com Editor instalado/ativado e suporte Windows Mono.

## Arquitetura

Oito asmdefs: Core sem Unity; Configuration depende Core; Input depende Core/InputSystem; Presentation depende Core/Configuration/Input/CM3/URP/TMP/uGUI; Runtime agrega os adaptadores; Editor prepara/builda; testes EditMode/PlayMode separados. Não criar ciclos nem dependência Core→Unity.

- `Core/FootballParameters.cs`: classe serializável de tuning e formação4-4-2 (11 slots normalizados). `Clone()` copia profundamente a formação. `Validate()` roda na inicialização; reflexão só fora do loop.
- `Core/FootballTypes.cs`: 22 estados de jogador fixos, estatísticas por time, fases e ações da bola.
- `Core/FootballMatch.cs`: lógica pura. Constructor recebe parameters, PitchGeometry e IRandomSource. Arrays/classes alocados uma vez. `Step(dt,ballPosition,ballVelocity,homeCommand,awayCommand,twoPlayers)` devolve `FootballBallAction`; o Rigidbody continua dono da bola.
- `Core/PlayerCommandBuffer.cs`: analog/held mais OR de bordas até Consume no passo fixo; Consume limpa somente bordas. Clear em pausa/reinício. Evita perder toques entre FixedUpdates ou repetir botões no catch-up.
- `Core/IFootballControls.cs`: interface de Match, pausa, reinício, modos e saída para UI.
- `Runtime/FootballBootstrap.cs`: preflight de configs/TMP/URP e transform identidade, cria campo/bola/22 corpos kinematic, amostra entrada Update em coordenadas da câmera, avança simulação60 Hz no FixedUpdate, aplica ações à bola e MovePosition aos atletas. Rigidbody interpolate; cápsulas têm raio Gameplay.PlayerRadius e altura FootballConfig.PlayerHeight. Guarda/restaura globais inclusive Time.timeScale; limpa mundo parcial em falha.
- `Presentation/FootballPlayerVisuals.cs`: modelos humanos originais de primitivas, materiais compartilhados, números TMP, corrida procedural e anéis de seleção. Só anima filhos visuais; não escreve posição/rotação dos roots físicos.
- `Presentation/FootballCameraRig.cs`: Wide lateral próxima, 70%bola/30%centro, smoothing/zoom. CM3 ForceCameraPosition e Brain ManualUpdate, inicializado antes de habilitar saída. Sem suposições de ordem automática do Brain.
- `Presentation/FootballHud.cs`: placar/relógio/status/stamina/carga, ajuda, pausa/menu, modo1P/2P/CPUdemo, nova partida/saída, remapeamento e5sliders. TMP numericSetText e aquecimento; alocações de criação/rebind/menu são permitidas, sem LINQ/new no loop estável. Em jogo, navegação UI e seleção estão desabilitadas para Sul não clicar em botões.

## Simulação e comportamento

Time 0 = mandante/Aurora FC azul, índices0..10; time 1 = Horizonte AC vermelho,11..21. Goleiros 0/11. Todos fictícios, FootballConfig.IsPlaceholder=true, números1..11; nenhum elenco/rating/dado oficial foi inventado.

Jogadores: aceleração/frenagem/giro limitado, sprint/stamina, separação por raio, suporte de formação. A IA tática pensa a cada0,1s. Escolhe perseguidor por tempo estimado de interceptação, com horizonte tunável e chegada desacelerada; perseguidor corre para não oscilar atrás de uma bola mais rápida que o jog. Suporte desloca com a bola e posse. Ações: passe por alinhamento/progresso/espaço/risco; profundidade com previsão; lançamento; chute carregado assistido, erro seeded e spin limitado por direção normalizada; bote. CPU pode mirar um canto oposto ao goleiro. Goleiros se posicionam e rebatem/afastam; não possuem animação de mergulho/encaixe.

A lógica lê a bola real antes de cada passo e retorna Teleport e/ou HasKick com posição/velocidade/spin. Drible são toques de velocidade cadenciados, sem parenting da bola. Core não integra PhysX nem promete determinismo entre plataformas. A avaliação swept de fronteira exige cruzamento de toda a esfera; contador de gol fica latched até reinício.

Relógio:300 s de bola em jogo para90min, dois tempos com troca de direção em halftime; pausas, gols e reposições adicionam tempo real. Gol inicia pequena pausa e saída do time que sofreu. Bola fora inicia **reposição de treino simplificada**, não lateral/escanteio/tiro de meta oficial. FullTime abre menu uma vez; Nova partida limpa placar/stats/stamina e preserva tuning da sessão.

## Entrada e modos

InputActions Gameplay: Move,Shoot,Pass,Through,Lob,Sprint,Switch,Pause. Gamepad: analógico,Sul,Oeste,Norte,Leste,RT,LB,Start. Teclado: WASD/setas,J,K,I,L,Shift,Tab,Esc. Movimento relativo à câmera. Sem pad, teclado paraP1; com pad, ele controlaP1. Dois pads para modo2P. SeP2 desconectar, voltaCPU. CPU×CPU usa CpuHome=true; modo1P tem CpuHome=false/TwoPlayers=false.

LocalInputHub tem cópia de asset por slot e filtros por dispositivo, overridesJSON no persistentDataPath, hotplug e rebind interativo. `HasDevice` permite fallback deP2. Esc também abre menu pelo Keyboard.current para acessibilidade com pad conectado; durante rebind ele cancela a captura em vez de abrir menu.

Pausa altera Time.timeScale=0 e cancela cargas/buffers. Ao continuar, Shoot fica suprimido até soltar para impedir chute vindo de Submit no menu. Estado global anterior é restaurado ao destruir o bootstrap.

## Tuning principal

`Resources/Config/Football.asset` contém gameplay e aparência. Assets originais ficam preservados; bootstrap usa clone. Sliders escrevem somente na cópia e mantêm min/max e jog<=sprint.

| Valores iniciais | Uso |
|---|---|
| Jog5,2 / sprint9 / acel16 / freio20 | Peso e resposta |
| Turn8 rad/s; stamina dreno0,055 / regen0,035 | Giro, fadiga |
| DribbleLead0,85m; interval0,12s; correction8 | Proximidade/controle da bola |
| Passe12..22 m/s; cone±30°; lead0,35 / through1,1s | Assistência e recepção |
| Chute18..32 m/s; charge0,95s; spin22; erro0,045rad | Potência/curva |
| AIdecision0,1s; reaction0,22s; intercept3s; arrival1,8m | Perseguição e chegada |
| Pressão3,2m; tacklechance0,25; passinterval3,6s | Ritmo da CPU |
| Keeperreach2,4m; height2,8m; clear24 m/s | Rebatidas simplificadas |
| Câmeraoffset(42,36,0); FOV55; damping4 | Leitura da ação |

SO Ball preserva massa0,43/raio0,11/Cd0,25/Magnus0,0015/restituição0,65/rolamento0,015. Pitch105×68/gol7,32×2,44. Simulation controla solver/fixed/gravity. Configurações fora de sliders exigem parar o Play e editarSO.

## Windows e atualização

`GERAR_E_JOGAR.exe` consulta branch→SHA, baixaZIP fixo com validação de caminhos, armazenaLocalAppData/Wynnyng/versions/<sha>, preserva clone e chama tools/Build-and-Play.ps1. Atualização é pelo preparador; atalho Jogar Wynnyng abre player de uma revisão já gerada. Novo banner menciona dois times/CPU; o preparador antigo também baixa o projeto atual.

Pipeline:prepare→EditMode→PlayMode→BuildPlayer StandaloneWindows64/Mono. WindowsBuild usa MatchScenePath exclusivamente; prepara SO Football, TMP, URP e cenas. Exige BuildReport sem erros +EXE/UnityPlayer.dll/_Data; escreve work/WindowsBuild/build-result.json, cria atalho e abre player. Logs têm progresso20s e erros C# extraídos.

GateXML aceita apenas o teste gráfico antigo ignorado em batch (nome/motivo exatos). Verifica casos/suítes, inclusive falha de teardown; outros ignores/inconclusivos/falhas ou zero aprovados bloqueiam. Nunca usar SkipTests para esconder uma falha nova. CIWindows valida PowerShell 7 e5.1, compila/testa preparador; não executa Unity.

## Testes e evidências

Executado localmente: **69 testes .NET aprovados**. Inclui23 casos FootballCore,3buffer,41Core anteriores e2checks alocação. CPU×CPU por600 s com integrador simplificado:0:0, chutes3:0, passes222:238, defesas0:2, posse117,277s:132,645s. Evidência de progresso/estabilidade, **não de balanceamento bom**; volume de finalizações baixo é risco conhecido. Cada loop medido de60.000 passos, após aquecimento, alocou0 bytes gerenciados na thread. Não cobre Unity/PhysX/CM/TMP/UI/render.

Preparados, **não executados aqui**:92 casos UnityEditMode e20PlayMode (19executáveis no batch,1pixel ignorado). Novos PlayMode verificam22corpos/bola física, movimentoCPU, gol único/reinício, pausa/restauração de globais, tuning isolado e apresentação/menu. Fixtures removem ambos os tipos de bootstrap antes de criar o seu para evitar cenas sobrepostas.

Checklist:ver22atletas/câmera/cores; mover/sprint/passe/chute/troca; CPU atacar/goleiro rebater; gol/reinício/saída; pausa congela; sliders/rebind; dois controles/hotplug; halftime/fulltime/NovaPartida; saída/reabertura; medirGPU/GC/FPS no player. Não registrar aceite Unity sem executar.

## Histórico de falhas preservadas como regressões

- Pacote:screen-capture→screencapture; validador de módulos mantém correção.
- TMP ausente:preflight antes de construirHUD; nenhuma atualização parcial/spam. Importar foraPlay.
- Campo invisível:guardaURP efetivo e inicialização da câmera antesBrain; usuário confirmou campo no player anterior.
- TagManager: layers vazios explicitamente strings, índicesGround8/Goal9/Ball10; validadorYAML/duplicatas/schema.
- CS0266 BuildSummary:errors/warnings sãoint, não uint, corrigido.
- AgregadoSkipped:Ignored:reprodução com DLL NUnit oficial mostrou13 aprovados+1ignorado; gate verifica folhas em vez de bloquear apenasagregado.
- NovaIA:no primeiro teste de 600 s não finalizava; perseguidores agora antecipaminterceptação, correm e desaceleram na chegada. O teste deprogresso permaneceuobrigatório.

## Pendências e limites

Não há regras completas/árbitro/impedimento/faltas/cartões/substituições; replays; áudio; modelos rigados/animaçãoHumanoid; estádio/torcida final; dadosBrasileirão/calendário/save/carreira/edição. Arte é blockout original, shaders unlit; goleiro/defesa/IA são simplificados. Sem Blender/Mixamo necessário. Não prometer60FPS na GTX 1060 sem medição, nem chamar preparador de player pronto.

Próximo trabalho deve partir da partida atual, testar no Unity real, corrigir erros observados e balancear ritmo/finalizações/posse antes de expandir modos ou arte. Usuário quer jogar e atualizar sem colar scripts; manter mesmo preparador e defaults prontos.
