window.aquaOps = {
    scrollToTop: () => window.scrollTo({ top: 0, behavior: 'smooth' }),
    // The login page can be statically rendered, so this tiny DOM helper works before a Blazor
    // circuit connects. It changes only the input presentation and never reads its value.
    togglePassword: (inputId, button) => {
        const input = document.getElementById(inputId);
        if (!input) return;
        const visible = input.type === 'password';
        input.type = visible ? 'text' : 'password';
        const icon = button.querySelector('[aria-hidden="true"]');
        if (icon) icon.textContent = visible ? '🙈' : '👁';
        button.setAttribute('aria-label', visible ? 'Ẩn mật khẩu' : 'Hiện mật khẩu');
        button.setAttribute('aria-pressed', visible ? 'true' : 'false');
    }
};
