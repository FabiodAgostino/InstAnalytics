@echo off
:: --- CONFIGURAZIONE CHIAVE ---
set "MY_MINIMAX_KEY=sk-cp-TUA_CHIAVE_QUI"
:: -----------------------------

:: 1. Cartella di configurazione ISOLATA per MiniMax
set "CLAUDE_CONFIG_DIR=%USERPROFILE%\.claude-minimax"
if not exist "%CLAUDE_CONFIG_DIR%" mkdir "%CLAUDE_CONFIG_DIR%"

:: 2. CREAZIONE DEL FILE DI CONFIGURAZIONE MCP (config.json)
:: Claude Code CLI spesso cerca 'config.json' nella root della config dir
set "MCP_CONFIG=%CLAUDE_CONFIG_DIR%\config.json"

echo [INFO] Configurazione Claude-Mem in corso...
(
    echo {
    echo   "mcpServers": {
    echo     "memory": {
      echo       "command": "npx",
      echo       "args": ["-y", "@modelcontextprotocol/server-memory"],
      echo       "env": { 
      echo         "MEMORY_FILE_PATH": "%CLAUDE_CONFIG_DIR:\=\\%\\knowledge-graph.json" 
      echo       }
    echo     }
    echo   }
    echo }
) > "%MCP_CONFIG%"

:: 3. Imposta le variabili per MiniMax
set "MY_MINIMAX_KEY=sk-cp-GYpl5xsTOQGvNcILxSJfDSH9Nf_ZJCFx0pxaRKz1KWIP89EpMTWCpwHTzKgEJNMkENSVq5a09ZZOQlXp_GXcoPF6uY6gm54_BB1jtciCVaINSPlXzsE84PA"
set "ANTHROPIC_BASE_URL=https://api.minimax.io/anthropic"
set "ANTHROPIC_API_KEY=%MY_MINIMAX_KEY%"
set "ANTHROPIC_MODEL=MiniMax-M2.7"
set "ANTHROPIC_STRIP_BETA_HEADERS=1"

:: 4. Si sposta nella cartella del progetto corrente
cd /d "%~dp0"

echo ==================================================
echo AVVIO CLAUDE CODE + MINIMAX + CLAUDE-MEM
echo ==================================================
echo [STATUS] Memoria attiva SOLO per questa istanza.
echo [PATH] %cd%
echo ==================================================

:: 5. Avvia Claude Code
claude --dangerously-skip-permissions


pause