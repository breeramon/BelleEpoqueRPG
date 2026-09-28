# Belle Époque RPG

RPG de batalha por turnos em Unity 6 (URP), com tema Belle Époque e horror, regras de **Ordem Paranormal** (PV, PE, Sanidade, NEX, testes com d20) e interface inspirada em *Clair Obscur: Expedition 33*.

## Como abrir

1. Instale a Unity **6000.6.3f1** (ou a mesma versão do `ProjectSettings/ProjectVersion.txt`) pelo Unity Hub, com o módulo Android Build Support se for testar no celular.
2. No Unity Hub: **Add > Add project from disk** e escolha esta pasta.
3. Espere a primeira importação (a pasta `Library/` é recriada localmente; ela não vai para o Git).
4. Abra `Assets/_Project/Scenes/Battle.unity` e aperte **Play**.
   - Se a cena não existir ou der erro, rode o menu **Belle Époque > 1. Montar projeto de exemplo**.

## Estrutura

| Pasta | Conteúdo |
| --- | --- |
| `Assets/_Project/Scripts/Core` | Regras em C# puro (Ordem Paranormal, turnos, dano, IA) |
| `Assets/_Project/Scripts/Data` | ScriptableObjects de habilidades, agentes e encontros |
| `Assets/_Project/Scripts/Presentation` | Controlador da batalha, câmera, animações, efeitos |
| `Assets/_Project/Scripts/UI` | HUD, tema e fontes |
| `Assets/_Project/Scripts/Editor` | Menu "Belle Époque" que monta dados e cena |
| `Assets/_Project/Tests/EditMode` | Testes (Window > General > Test Runner) |

Fontes: Cinzel, EB Garamond e Bebas Neue (SIL Open Font License).
