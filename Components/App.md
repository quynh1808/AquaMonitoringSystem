# App.razor

HTML shell của Blazor Web App.

## Trách nhiệm

- Khai báo ngôn ngữ, viewport và base URL.
- Tải `app.css`, `app.js` và favicon thông qua `@Assets[...]` của static asset pipeline.
- Render `HeadOutlet` và `Routes` ở chế độ Interactive Server.
- Tải `blazor.web.js` để thiết lập kết nối Blazor.

Dùng `@Assets[...]` giúp đường dẫn đúng khi .NET áp dụng fingerprint hoặc publish static assets.
