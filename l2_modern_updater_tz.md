# Техническое задание
## Современный аналог L2 LameUpdater

### Проект
Разработка современного лаунчера и системы обновления клиента для Lineage II.

---

# 1. Цели проекта

Создать:
- современный launcher/updater;
- безопасную систему обновлений;
- HTTPS-совместимую архитектуру;
- поддержку CDN;
- защиту от поврежденных файлов;
- автообновление лаунчера;
- многопоточную загрузку;
- современный UI;
- совместимость с Windows 10/11.

---

# 2. Архитектура

## 2.1 Компоненты

### Клиент
Launcher.exe

Функции:
- обновление клиента;
- проверка файлов;
- загрузка патчей;
- запуск игры;
- логирование;
- авторизация;
- отображение новостей;
- self-update.

### Backend API
REST API.

Функции:
- выдача manifests;
- авторизация;
- CDN routing;
- версии клиента;
- статистика.

### Patch Storage
Хранение файлов:
- CDN;
- S3;
- Nginx;
- Cloudflare R2.

---

# 3. Технологии

## Launcher
Рекомендуется:
- C#
- .NET 8
- WPF / AvaloniaUI

Альтернативы:
- Electron
- Qt
- C++

---

# 4. Основной функционал

## 4.1 Проверка файлов

Launcher должен:
- сканировать клиент;
- считать SHA256;
- сравнивать с manifest.

## 4.2 Обновление

Поддержка:
- многопоточной загрузки;
- resume download;
- delta patches;
- ограничение скорости.

## 4.3 HTTPS

Обязательно:
- TLS 1.2+
- сертификаты Let's Encrypt;
- проверка SSL.

---

# 5. Manifest Format

Пример:

```json
{
  "version": "1.0.0",
  "files": [
    {
      "path": "system/l2.exe",
      "sha256": "HASH",
      "size": 123456,
      "url": "https://cdn.site.com/system/l2.exe"
    }
  ]
}
```

---

# 6. Безопасность

## Обязательно

- HTTPS only;
- SHA256 verification;
- anti-tamper;
- защита от MITM;
- цифровая подпись manifests.

## Дополнительно

- античит интеграция;
- HWID;
- защита от DLL injection.

---

# 7. UI/UX

## Главное окно

Содержит:
- кнопку Play;
- статус обновления;
- progress bar;
- новости;
- скорость загрузки;
- лог ошибок.

---

# 8. Self-Update

Launcher должен:
1. проверять свою версию;
2. скачивать новый bootstrap;
3. перезапускаться автоматически.

---

# 9. Серверная часть

## API endpoints

### GET /manifest.json

Возвращает manifest.

### GET /version

Текущая версия.

### POST /auth

Авторизация.

---

# 10. CDN

Поддержка:
- Cloudflare;
- BunnyCDN;
- AWS CloudFront.

---

# 11. Логирование

Launcher пишет:
- launcher.log
- updater.log
- crash.log

---

# 12. Производительность

Требования:
- запуск < 2 секунд;
- проверка 50k файлов < 30 секунд;
- поддержка клиентов 100k+.

---

# 13. Совместимость

## ОС
- Windows 10
- Windows 11

## Архитектура
- x64

---

# 14. Дополнительные модули

## Опционально
- встроенный браузер;
- Discord RPC;
- Telegram notifications;
- автоочистка кеша;
- repair mode.

---

# 15. Админ-панель

Возможности:
- загрузка patch build;
- генерация manifest;
- rollback;
- просмотр статистики.

---

# 16. CI/CD

Pipeline:
- build launcher;
- sign binaries;
- upload CDN;
- generate manifests.

---

# 17. Этапы разработки

## Stage 1
MVP:
- updater;
- manifest;
- launcher UI.

## Stage 2
- HTTPS;
- self-update;
- многопоточность.

## Stage 3
- auth;
- anti-cheat;
- analytics.

---

# 18. Структура проекта

```text
Launcher/
Backend/
PatchBuilder/
AdminPanel/
CDN/
```

---

# 19. Требования к качеству

- отсутствие corruption;
- отказоустойчивость;
- корректное восстановление после обрыва загрузки.

---

# 20. Итог

Проект должен заменить:
- LameUpdater;
- старые FTP patchers;
- HTTP-only launchers.

И предоставить:
- современную архитектуру;
- безопасность;
- высокую скорость обновления;
- поддержку HTTPS/CDN.
