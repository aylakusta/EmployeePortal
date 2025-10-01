# EmployeePortal (SQLite, Admin Panel, Business UI)

## Özellikler
- SQLite veritabanı (`app.db`)
- Identity ile kullanıcı/rol yönetimi (seed: `admin@portal.com / Admin123!`, `user@portal.com / User123!`)
- Admin panel (kullanıcı listesi, admin rolü aç/kapat)
- Bordro: Sadece PDF listeleme/yükleme (ücret görünmez)
- Bootstrap 5 + AdminLTE (CDN) ile iş seviyesinde arayüz

## Çalıştırma
```bash
cd WebUI
dotnet restore
dotnet ef migrations add InitialCreate
dotnet ef database update
dotnet run
```
Tarayıcı: https://localhost:5001 veya http://localhost:5000

## Notlar
- Bordro PDF'leri `wwwroot/uploads/payrolls` altında saklanır.
- Admin paneline erişmek için admin hesabıyla giriş yapın.
