# Validação da entrega — Fases 0 + 1A

## Executado neste ambiente

Ambiente Linux, .NET SDK **8.0.428**, sem Unity Editor instalado/licenciado. Inspeção inicial do repositório remoto: vazio, sem commits. Todos os arquivos desta entrega são novos.

| Verificação | Resultado real |
|---|---|
| Compilação do núcleo | **Passou**, C# 9, .NET Standard 2.1, warnings tratados como erros |
| Testes do núcleo em .NET 8 | **42 passaram, 0 falhas, 0 ignorados** |
| Alocação do núcleo após aquecimento | **0 bytes gerenciados** em 60.000 passos, incluindo física pura, sessão, RNG e publicação de eventos |
| Sintaxe dos scripts de Assets | **24 arquivos, 0 erros** com Roslyn/C# 9; não verifica tipos do motor |
| Estrutura do projeto | **Passou**: 55 GUIDs únicos de assets/pastas, 8 assemblies sem ciclos, referências e IDs das ações válidos |
| APIs antigas / código incompleto | Sem usos de Input legado, `FindObjectOfType`, `FindObjectsOfType`, LINQ ou marcadores de implementação pendente nos scripts |
| Espaços/diff | `git diff --check` sem problemas; verificação repetida após adicionar os arquivos ao índice |

Os 42 testes incluem **41 casos compartilhados com o EditMode** e **1 teste adicional de alocação** do runner .NET. O teste de alocação não cobre PhysX, renderização, Input System, logging interno do Editor ou o funcionamento interno do TMP/uGUI.

Comandos reproduzíveis:

```sh
python3 tools/validate_project.py
python3 -m unittest discover -s tools -p 'test_*.py'
dotnet test tools/HeadlessChecks/HeadlessChecks.csproj --configuration Release
```

A CI em `.github/workflows/core-tests.yml` também executou com **sucesso** no GitHub: [Core CSharp tests, execução 43674594509](https://github.com/YokiPJ/Wynnyng-six-seven/actions/runs/43674594509), referente ao commit de código `94017c7d4df46b2b4788ff29a020bd02b66194ca`. O XML TRX fica no artefato `core-test-results` dessa execução. Essa CI não exige licença Unity e não executa o motor.

## Correção após a abertura do usuário

O screenshot de **Package Manager Error** confirmou uma falha do manifest inicial: `com.unity.modules.screen-capture@1.0.0` não existe. Foi corrigido para [`com.unity.modules.screencapture@1.0.0`](https://docs.unity3d.com/6000.0/Documentation/Manual/com.unity.modules.screencapture.html).

As páginas do manual Unity 6 dos outros 23 módulos declarados foram consultadas e existem sob os nomes usados. Isso confirma nomenclatura; a resolução efetiva dentro do Editor continua sendo um aceite separado. A CI anterior verificava somente os cinco pacotes principais, sem verificar os nomes dos módulos embutidos, e por isso deixou o erro passar.

O validador agora inclui nomes e versões dos módulos. Quatro testes Python verificam a rejeição do erro original, a aceitação do nome correto, uma versão inválida e o manifest entregue. O README descreve a correção local de uma linha e como abrir a cena certa após a resolução dos pacotes. Nenhum sistema da Fase 1B foi iniciado.

## Preparado, sem execução neste ambiente

| Aceite | Como verificar |
|---|---|
| Compilação Unity 6 completa, sem erros/warnings | Abrir no Editor 6000.0.62f1; aguardar importação e conferir Console |
| Resolução efetiva dos pacotes | Package Manager; primeiro import gera `packages-lock.json` |
| EditMode | Test Runner: 44 casos esperados, incluindo 3 testes de configuração/entrada que dependem do motor |
| PlayMode | Test Runner: 9 testes de integração, descritos no README |
| HLSL, materiais e layout da interface | Inspeção em Play no Editor com renderização habilitada |
| Hotplug, remapeamento e dois controles físicos | Checklist manual do README |
| 60 FPS / GTX 1060 / GC da integração | Development Build Windows e Profiler após aquecimento |

**O aceite do Unity permanece aberto.** Testes .NET e inspeção de fontes não equivalem a compilar/executar o projeto no Editor. Não há medição de GPU, draw calls, FPS ou PhysX feita nesta entrega.

## APIs conferidas em fontes oficiais

As assinaturas principais foram consultadas; as fontes das branches oficiais podem evoluir. Isso reduz risco de API inventada, mas não substitui a compilação com os binários do alvo.

- Versão/revisão do Editor: [API de releases Unity 6000.0.62f1](https://services.api.unity.com/unity/editor/release/v1/releases?version=6000.0.62f1&limit=1).
- `Rigidbody.linearVelocity`, `linearDamping`, `angularDamping`: [UnityCsReference, Rigidbody](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.0/Modules/Physics/ScriptBindings/Rigidbody.bindings.cs).
- `PhysicsMaterial` e `PhysicsMaterialCombine`: [UnityCsReference, PhysicsMaterial](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.0/Modules/Physics/ScriptBindings/PhysicsMaterial.bindings.cs).
- `SimulationMode.FixedUpdate`: [UnityCsReference, Physics](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.0/Modules/Physics/ScriptBindings/Physics.bindings.cs).
- `GraphicsSettings.defaultRenderPipeline`: [UnityCsReference, GraphicsSettings](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.0/Runtime/Export/Graphics/GraphicsSettings.bindings.cs).
- `UniversalRenderPipelineAsset.Create`, `msaaSampleCount`, `shadowCascadeCount`, `useSRPBatcher`: [URP 6000.0](https://github.com/Unity-Technologies/Graphics/blob/6000.0/staging/Packages/com.unity.render-pipelines.universal/Runtime/Data/UniversalRenderPipelineAsset.cs).
- `CinemachineCamera.Lens` e `Unity.Cinemachine`: fontes do [pacote oficial Cinemachine 3.1.3](https://download.packages.unity.com/com.unity.cinemachine/-/com.unity.cinemachine-3.1.3.tgz), consultadas fora do projeto.
- Rebinding, filtros de devices e `InputSystemUIInputModule.AssignDefaultActions`: fontes do [pacote oficial Input System 1.11.2](https://download.packages.unity.com/com.unity.inputsystem/-/com.unity.inputsystem-1.11.2.tgz), consultadas fora do projeto.
- Overloads de `TMP_Text.SetText`: [fontes oficiais uGUI/TMP](https://github.com/Unity-Technologies/uGUI/blob/6000.0/com.unity.ugui/Runtime/TMP/TMP_Text.cs).

As fontes consultadas não foram copiadas para o repositório. A entrega usa APIs públicas e implementação própria de simulação, geometria e integração.
