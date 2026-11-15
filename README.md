# Wynnyng — Fases 0 e 1A

Projeto original de futebol 3D para PC/gamepad. Este pacote implementa a base do projeto e o laboratório de campo/bola. O controle de um jogador começa na Fase 1B, somente depois do comando **“próxima”**.

**Para gerar e abrir no Windows:** feche o Editor e execute [GERAR_E_JOGAR.exe](GERAR_E_JOGAR.exe). O preparador baixa a revisão mais recente, usa seu Unity instalado, prepara TMP/URP, roda EditMode/PlayMode e gera `Wynnyng.exe`. O player Unity ainda será compilado no seu PC; o arquivo entregue é o preparador. [Instruções e limites](docs/WINDOWS.md).

**Suposição:** Unity **6000.0.62f1 (Unity 6.0 LTS)**, Windows, 1 unidade = 1 metro, campo no plano XZ, +Y para cima. Sem Blender, Mixamo ou assets de outro jogo.

O projeto foi escrito para essa versão. **O Editor Unity não estava disponível no ambiente de desenvolvimento: a compilação no Unity, os testes EditMode/PlayMode e o desempenho na GTX 1060 ainda precisam ser executados.** Os testes do núcleo C# foram executados em .NET; veja [VALIDACAO.md](docs/VALIDACAO.md).

## 1. Plano e entregas

1. Fixar versão do Editor e dependências compatíveis.
2. Separar núcleo C# puro, configurações, entrada, apresentação, runtime e Editor por asmdef.
3. Criar SOs editáveis de simulação, campo, bola e apresentação.
4. Preparar Input Actions remapeáveis e cópias independentes para dois jogadores locais.
5. Implementar bootstrap, eventos tipados, logging e RNG injetável com seed.
6. Gerar campo, marcações, gols e redes usando geometria procedural.
7. Integrar Rigidbody da bola com arrasto quadrático, Magnus, rolamento e quique.
8. Detectar gol/saída pelo cruzamento completo da esfera e oferecer uma bancada de lançamentos.
9. Entregar testes, checklist manual, parâmetros e HANDOFF.

## 2. Arquivos e código completo

A árvore completa está em [docs/ARQUIVOS.md](docs/ARQUIVOS.md). Os arquivos em `Assets/Wynnyng/` contêm o código completo; não há trechos para copiar por cima de scripts incompletos.

```text
Assets/Wynnyng/
  Core/             # C# puro, sem referências ao motor
  Configuration/    # ScriptableObjects de tuning
  Input/            # Input System, dispositivos e remapeamento
  Presentation/     # Malhas, câmera Cinemachine 3 e diagnóstico uGUI/TMP
  Runtime/          # Bootstrap, Rigidbody e ponte com a simulação
  Editor/           # Preparação idempotente de assets/cena/URP/TMP
  Resources/
    Config/         # Simulation, Pitch, Ball e Presentation.asset
    Input/          # Match.inputactions
    Shaders/        # HLSL URP original: campo e bola
  Scenes/           # Phase01A.unity
  Tests/
    EditMode/       # Núcleo + configurações + contrato dos controles
    PlayMode/       # Aceite da integração física
Packages/manifest.json
ProjectSettings/
tools/              # Validação estrutural, núcleo .NET e testes
HANDOFF.md
```

Dependências diretas:

| Pacote | Versão |
|---|---|
| Universal RP | 17.0.4 |
| Input System | 1.11.2 |
| Cinemachine | 3.1.3, namespace `Unity.Cinemachine` |
| Unity UI | 2.0.0, inclui TextMeshPro |
| Test Framework | 1.4.5 |

Não instalar o antigo pacote independente `com.unity.textmeshpro`. O Editor gera `packages-lock.json` ao resolver os pacotes; não foi criado um lock fictício sem essa resolução.

## 3. Passos manuais no Editor

1. No Unity Hub, instale **6000.0.62f1** e o módulo **Windows Build Support (Mono)** se quiser gerar um executável mais tarde.
2. Extraia o ZIP, se necessário. Em **Projects → Add → Add project from disk**, escolha a pasta que contém `Assets`, `Packages` e `ProjectSettings`.
3. Abra o projeto e aguarde o Package Manager terminar. Se o Editor pedir reinício da entrada, escolha **Input System Package (New)** e reinicie.
4. No menu superior, clique em **Wynnyng → Preparar Fases 0 e 1A**. Aguarde a mensagem **Ready** na Console. O comando importa TMP Essential Resources da instalação do Unity e cria/configura o URP. Não requer baixar modelos ou fontes por fora.
5. Clique em **Wynnyng → Abrir laboratorio Fase 1A**.
6. Na aba **Game**, selecione proporção **16:9** e aperte **Play**.
7. Conecte um gamepad. Use **RB** para lançar a bola, **D-pad esquerda/direita** para escolher o caso e **Select/View** para repor. Alternativa: setas, **Enter** e **R**.
8. Abra **Controles** com **Start/F1** ou com o botão na tela. Escolha o jogador, selecione um comando e pressione o novo controle. **Esc** cancela. Os remapeamentos ficam salvos neste PC.
9. Para ajustar os valores nesta fase, pare o Play e selecione um asset em `Assets/Wynnyng/Resources/Config/`. Depois aperte Play novamente.

O comando de preparação preserva configurações/cena que já existem e preserva outras cenas na lista de build. Ele aplica o URP criado a todos os níveis de qualidade. Execute-o novamente se apagar os assets gerados em `Assets/Wynnyng/Rendering/`.

Na versão corrigida, **Wynnyng → Preparar e iniciar laboratorio** faz a preparação, aguarda recursos essenciais quando necessário, abre `Phase01A` e entra em Play. Use o comando com o Play parado.

O campo nasce ao entrar em Play. É esperado que a Scene esteja quase vazia fora do Play. Mantenha `Wynnyng Bootstrap` na origem, sem rotação e com escala 1.

Se você baixou a primeira entrega e apareceu **Package Manager Error** dizendo que `com.unity.modules.screen-capture` não existe:

1. Feche o Editor; se a janela de erro estiver aberta, clique em **Quit**.
2. Na pasta do projeto, abra `Packages/manifest.json` no Bloco de Notas.
3. Troque somente `com.unity.modules.screen-capture` por `com.unity.modules.screencapture`, preservando `"1.0.0"` e a vírgula.
4. Salve e reabra o projeto pelo Hub. O nome já está corrigido na branch `codex/phase-0-1a`.
5. Execute **Wynnyng → Preparar Fases 0 e 1A**, depois **Wynnyng → Abrir laboratorio Fase 1A**, e aperte **Play** na aba **Game**. A cena `Untitled` com câmera/luz não é o laboratório.

Se a cena do laboratório abrir preta e a Console mostrar `TMP_Settings.get_defaultFontAsset` / erros repetidos em `LaboratoryHud.Update`:

1. Pare o **Play** pelo botão azul no topo. TMP Essential Resources só pode ser importado fora do Play; o erro `Cannot import package in play mode` indica que a importação foi tentada durante a execução.
2. Use **Window → TextMeshPro → Import TMP Essential Resources**. Em algumas instalações o submenu aparece como **Text Mesh Pro**. Importe os recursos essenciais e aguarde terminar.
3. Execute **Wynnyng → Preparar Fases 0 e 1A** e aguarde **Ready**.
4. Na Console, clique em **Clear** para remover as mensagens antigas. Entre em Play novamente na cena `Phase01A`.

O código corrigido verifica os settings/fontes antes de criar o mundo e só ativa o HUD depois de construir todos os seus componentes. Uma falha de inicialização limpa o mundo parcial e restaura os parâmetros globais. O comando de preparação não informa Ready enquanto a fonte padrão não estiver carregada. Atualizar a branch traz essas proteções; importar TMP fora do Play resolve os recursos ausentes também na cópia anterior.

Se os textos e o contador aparecem, mas o campo continua escuro:

1. Pare o Play.
2. Execute **Wynnyng → Preparar Fases 0 e 1A** e aguarde **Ready**.
3. Confira se apareceu `Assets/Wynnyng/Rendering/`, com `WynnyngURP` e `WynnyngRenderer`.
4. Abra o laboratório e entre em Play novamente. Na versão atualizada também é possível usar **Preparar e iniciar laboratorio**.

Os shaders do campo são de URP. O HUD aparecer sozinho não comprova que o pipeline 3D foi configurado. A nova verificação considera tanto Graphics quanto o override da qualidade atual e interrompe o startup com orientação explícita se nenhum URP estiver selecionado. A câmera é configurada como Base, com renderer, culling e projeção definidos, e seu estado Cinemachine é preparado antes de ativar o Brain.

## 4. Controles preparados para a Fase 1B

Estes comandos já existem no asset de entrada. Nesta entrega, somente os comandos do laboratório e o menu de remapeamento produzem ações visíveis.

| Ação futura | Gamepad | Teclado de apoio |
|---|---|---|
| Mover | Analógico esquerdo | WASD |
| Chutar / bote | Sul | J |
| Passe / pressão | Oeste | K |
| Profundidade / saída do goleiro | Norte | I |
| Lançamento / cruzamento / carrinho | Leste | L |
| Sprint | Gatilho direito | Shift esquerdo |
| Troca / tabela | Ombro esquerdo | Tab |
| Pausa | Start | Esc |

O primeiro gamepad disponível vai para P1, o segundo para P2. Os slots mantêm dispositivos ainda conectados; desconectar P1 não rouba o controle de P2. Sem gamepad em P1, o teclado é usado. Sem dispositivo em P2, seus comandos são neutros. O menu é compartilhado; gameplay de dois jogadores pertence às fases seguintes.

O remapeamento aceita somente o dispositivo do slot selecionado. Salva overrides JSON em `Application.persistentDataPath/bindings-player-0.json` e `bindings-player-1.json`. O botão **Padrao** restaura o slot selecionado.

## 5. Testes automatizados e checklist manual

No Editor:

1. Pare o Play.
2. Abra **Window → General → Test Runner**.
3. Em **EditMode**, execute **Run All**.
4. Em **PlayMode**, execute **Run All**. Os testes criam seu próprio laboratório e o removem ao terminar.
5. Verifique ausência de erros e warnings na Console. Qualquer falha de importação/compilação significa que o aceite Unity ainda não foi atendido.

Sem Unity, com Python 3 e .NET SDK 8 instalados:

```sh
python3 tools/validate_project.py
python3 -m unittest discover -s tools -p 'test_*.py'
dotnet test tools/HeadlessChecks/HeadlessChecks.csproj -c Release
```

O projeto `tools/CoreLibrary/` compila os mesmos fontes do núcleo para **.NET Standard 2.1**, perfil compatível com o alvo Unity. O runner .NET executa os mesmos testes puros do EditMode e um teste adicional de alocação. Ele não simula PhysX e não compila os scripts que dependem do motor.

Antes da validação Python, instale `python3 -m pip install -r tools/requirements-validation.txt`. Ela confere o YAML textual, incluindo 32 layers com strings vazias explícitas; não executa o parser nativo do Unity. Os testes de configuração também rejeitam NaN/Infinity, e o bootstrap verifica origem/rotação/escala antes de construir o campo.

Opcional, PowerShell com o Editor fechado e licenciado:

```powershell
./tools/run-unity-tests.ps1 -EditorPath 'C:/Program Files/Unity/Hub/Editor/6000.0.62f1/Editor/Unity.exe'
```

Saída: `work/UnityTests/`, com XML e logs de preparação/EditMode/PlayMode. Uma execução `-nographics` não substitui a inspeção visual dos shaders.

Cobertura preparada:

- Núcleo: cruzamento completo da esfera, dois gols, altura/largura da abertura, interpolação do ponto de cruzamento, altas velocidades, canto e primeira linha cruzada, trava de evento e reset.
- Física pura: fórmula de arrasto, direção de Magnus, resistência sem inverter velocidade, rolamento, parâmetros inválidos e valores não finitos.
- Infraestrutura: seed reproduzível, seed zero definida, relógio, capacidade/inscrição/remoção de eventos e contagem única por bola em jogo.
- EditMode do Unity: SOs, recursos, ações necessárias e isolamento/serialização dos remapeamentos.
- EditMode de TMP: ausência dos settings sem acessar o getter inseguro, ausência da fonte padrão e fonte carregada após preparação.
- EditMode de renderização: pipeline ausente e configuração URP efetiva após preparação.
- PlayMode do Unity: bootstrap/60 Hz, dimensões e componentes, gol nos dois sentidos, bola acima da trave, lateral, colisão na trave, quique, curva e reset; HUD rejeitado sem erros por frame, inicialização com apresentação completa e nova tentativa após falha.
- PlayMode gráfico: lê quatro pontos do gramado na imagem e os compara ao fundo. Execute com a aba **Game visível**; esse caso é ignorado em batch/nographics e não é validado pela CI headless.

Checklist manual — marque depois de executar no seu PC:

- [ ] Projeto abre e compila sem erros nem warnings.
- [ ] TMP Essential Resources importado com o Play parado; preparação só informa Ready quando a fonte padrão existe.
- [ ] Campo 105 × 68 m com perímetro, meio, círculo central, áreas, marcas de pênalti, arcos e cantos.
- [ ] Dois gols com abertura livre 7,32 × 2,44 m; traves colidem; redes seguram a bola.
- [ ] Campo, bola, traves e textos aparecem com as cores corretas, sem materiais rosa.
- [ ] URP selecionado em Graphics e no nível de qualidade atual; a preparação cria os dois assets na pasta Rendering.
- [ ] `Gol no fundo +Z` e `Gol no fundo -Z` somam exatamente um evento cada.
- [ ] `Bola acima do travessao` conta fora, sem contar gol.
- [ ] `Saida pela lateral` cruza a linha sem bater numa parede invisível.
- [ ] `Quique de 3 metros` rebate e perde altura a cada quique.
- [ ] `Curva / Magnus` desvia para +X com spin positivo em Y.
- [ ] `Rolamento` desacelera; `Colisao com a trave` rebate sem gol.
- [ ] A bola volta ao centro após 3 s de gol/saída; repor manualmente preserva os contadores.
- [ ] Remapeamento funciona por mouse/teclado e por navegação do gamepad; continua após fechar/abrir o jogo.
- [ ] Dois gamepads possuem remapeamentos independentes; desconexão/reconexão não gera exceções.
- [ ] EditMode e PlayMode passam no Test Runner.
- [ ] Com recursos TMP ausentes, a tentativa de Play relata uma falha de preparação, sem gerar `NullReferenceException` a cada frame; depois de importar os recursos fora do Play, a cena inicia normalmente.
- [ ] Com URP ausente, o startup orienta preparar o projeto; depois da preparação, o gramado aparece também na imagem capturada pelo teste gráfico.
- [ ] Num Development Build Windows, medir CPU/GPU/GC pelo Profiler, após aquecimento, com captura da máquina e resolução. Meta: 60 FPS; validação na GTX 1060 ainda pendente.

## 6. Tuning e sensação

Mude os SOs com o Play parado nesta fase. Os sliders em runtime chegam na **Fase 1B**, junto ao jogador, passe e chute.

| Asset / campo | Inicial | Como afeta o teste |
|---|---:|---|
| Ball / Mass | 0,43 kg | Base física; mudar altera a desaceleração aerodinâmica |
| Ball / Radius | 0,11 m | Diâmetro de 0,22 m; muda collider e teste da esfera inteira |
| Ball / DragCoefficient | 0,25 | Menor: bola conserva velocidade por mais tempo |
| Ball / MagnusCoefficient | 0,0015 | Maior: mais curva para o mesmo spin em rad/s |
| Ball / Restitution | 0,65 | Maior: quique mais alto |
| Ball / RollingFriction | 0,015 | Maior: para mais cedo no chão |
| Ball + Pitch / SurfaceFriction | 0,18 | Troca entre deslizamento e rolamento no contato |
| Ball / AngularDamping | 0,05 | Maior: spin se dissipa mais rápido |
| Simulation / BounceThreshold | 0,2 m/s | Evita microquiques abaixo desse impacto |
| Pitch / BoundaryContactTolerance | 0,002 m | Tolera pequena penetração numérica no chão/abertura, sem antecipar a linha |
| Presentation / Scenarios | Vetores editáveis | Velocidades de teste: 12/18/22/24/32 m/s; spin e altura livres |
| Presentation / AutoResetDelay | 3 s | Tempo para observar gol/saída antes de repor |
| Presentation / OverviewPosition e FieldOfView | (78,72,0), 68° | Vista geral estática para esta bancada |
| Simulation / Seed | 2026 | Fonte injetada; lançamentos atuais não usam erro aleatório |

Para comparar: mantenha o mesmo cenário e altere um parâmetro de cada vez. Primeiro ajuste arrasto/rolamento; depois quique; por último Magnus. Não altere o passo fixo: o contrato desta arquitetura é **60 Hz**. `TargetFrameRate` limita a renderização; o Rigidbody interpola entre passos.

## 7. Limites e continuidade

- O aceite de compilação/PlayMode/visual/performance no Unity permanece pendente. A verificação .NET não é evidência de execução do motor.
- PhysX usa passo fixo e a lógica possui RNG reproduzível, mas não há promessa de determinismo entre máquinas ou versões do motor.
- Redes são grades visuais com colliders rígidos de baixa restituição; não simulam tecido. Traves usam cápsulas como blockout.
- `Goal`, `Touchline` e `GoalLine` são detecções. Posse, último toque, escanteio, tiro de meta e reinício oficial pertencem à Fase 3. Os contadores +Z/−Z ainda não representam clubes.
- A vista Cinemachine 3 atual é fixa. Wide com alvo ponderado e zoom, jogador e painel de sliders são Fase 1B; outras câmeras/replay ficam nas fases previstas.
- `ReadPlayer` é um snapshot do frame de entrada. A Fase 1B deve armazenar/consumir bordas de botões na transição para o passo fixo.
- Logging e uGUI/TMP são diagnósticos. Zero alocação foi medido no núcleo .NET após aquecimento; a integração inteira precisa de medição no Profiler do Unity.
- Clubes, elencos, importador CSV, calendário e regras de campeonato ficam na Fase 6. Nenhum elenco, rating, transferência ou dado oficial foi inventado nesta entrega. A lista Série A 2026 fornecida pelo usuário ainda precisa de conferência na fase de dados.
- Modelos, animações, uniformes, estádio e pipeline Blender ficam na Fase 4. Não são necessários para abrir esta bancada.

[HANDOFF.md](HANDOFF.md) registra arquitetura, decisões, convenções e o estado de cada sistema para retomar em um novo chat.
