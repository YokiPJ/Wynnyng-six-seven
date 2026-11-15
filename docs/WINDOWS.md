# Windows — atualizar e jogar a partida

1. Feche o player antigo e todas as janelas do Unity.
2. Abra `GERAR_E_JOGAR.exe`. O mesmo arquivo já baixado busca o HEAD atual da branch `codex/phase-0-1a`.
3. Aguarde preparação, EditMode, PlayMode e build. O jogo abre quando tudo conclui.

O preparador gera a partida **Aurora FC × Horizonte AC**, com 11 jogadores por time e adversário CPU. O novo player começa em `FootballMatch.unity`; o laboratório antigo continua no projeto para diagnóstico.

**O EXE entregue é um preparador .NET Framework 4.8, não um player Unity pré-compilado.** O player é compilado no seu PC com Unity **6000.0.62f1** instalado/ativado e Windows Build Support (Mono). A primeira execução precisa de internet e pode levar vários minutos para importar pacotes. A janela informa a etapa, o tempo e o caminho do log.

O download usa um SHA fixo em `%LOCALAPPDATA%/Wynnyng/versions/<sha>/`; o clone original permanece preservado. Na mesma revisão, um player completo com recibo de sucesso é aberto diretamente. Uma revisão nova passa novamente por preparação/testes/build.

## Arquivos gerados

- Player: `Builds/Windows/Wynnyng.exe`, acompanhado de `UnityPlayer.dll` e `Wynnyng_Data`.
- Atalho: **Jogar Wynnyng**, na área de trabalho, aponta para o player dessa revisão.
- Logs/relatórios: `work/WindowsBuild/prepare.log`, `EditMode.log/.xml`, `PlayMode.log/.xml`, `build.log`, `build-result.json`.

Use o preparador para atualizar; o atalho abre a revisão já instalada. Mantenha toda a pasta do player, não só o EXE.

## Verificações automáticas

Preparação importa TMP Essential Resources, configura URP e valida os SOs. O build usa somente a cena da partida e confirma BuildReport, EXE, DLL e `_Data` antes de escrever sucesso/abrir o player. EditMode e PlayMode são obrigatórios por padrão.

O NUnit pode produzir agregado `Skipped:Ignored` quando o único teste de pixels não roda em batch. O script verifica cada caso e suíte, aceitando somente o nome e motivo exatos desse teste gráfico conhecido. Outros ignorados, inconclusivos, ausência de aprovados ou falhas interrompem a geração. Nomes e motivos aparecem na janela, assim como erros C# extraídos do log.

Nenhum Editor/licença Unity está disponível no ambiente de desenvolvimento. A CI Windows verifica o preparador e os scripts em PowerShell 7/5.1; isso não executa os testes do motor nem inspeciona a imagem do jogo.

## Comandos para desenvolvimento

Com o clone atualizado e o Editor fechado:

```powershell
./GERAR_E_JOGAR.cmd
./tools/Build-and-Play.ps1 -EditorPath 'C:/Program Files/Unity/Hub/Editor/6000.0.62f1/Editor/Unity.exe'
./tools/Build-and-Play.ps1 -NoLaunch
```

`-SkipTests` existe somente para uma solicitação explícita e não é usado pelo preparador.

Para recompilar o preparador, com SDK .NET 8:

```powershell
dotnet build tools/WindowsBootstrap/WindowsBootstrap.csproj -c Release -o work/bootstrap
Copy-Item work/bootstrap/GERAR_E_JOGAR.exe .
Copy-Item work/bootstrap/GERAR_E_JOGAR.exe.config .
./GERAR_E_JOGAR.exe --self-test
pwsh -File tools/Test-BuildPipeline.ps1
```

Esses comandos compilam/testam o preparador; gerar o jogo requer o Unity.
