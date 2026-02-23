(function() {
    // Create toggle button
    const toggleButton = document.createElement('button');
    toggleButton.className = 'dark-mode-toggle';
    toggleButton.innerHTML = '🌙 Dark Mode';
    
    // Check for saved preference
    const savedMode = localStorage.getItem('swagger-dark-mode');
    if (savedMode === 'true') {
        document.body.classList.add('dark-mode');
        toggleButton.innerHTML = '☀️ Light Mode';
    }
    
    // Toggle functionality
    toggleButton.addEventListener('click', function() {
        document.body.classList.toggle('dark-mode');
        const isDarkMode = document.body.classList.contains('dark-mode');
        localStorage.setItem('swagger-dark-mode', isDarkMode);
        toggleButton.innerHTML = isDarkMode ? '☀️ Light Mode' : '🌙 Dark Mode';
    });
    
    // Add button to page when DOM is ready
    if (document.body) {
        document.body.appendChild(toggleButton);
    } else {
        document.addEventListener('DOMContentLoaded', function() {
            document.body.appendChild(toggleButton);
        });
    }
})();
