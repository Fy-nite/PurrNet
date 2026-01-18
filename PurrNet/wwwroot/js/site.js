// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.



// Theme management
document.addEventListener('DOMContentLoaded', function() {
    // Check if we're in testing mode (passed from server)
    const isTestingMode = document.body.getAttribute('data-testing-mode') === 'true';
    if (isTestingMode) {
        console.log('🧪 PurrNet is running in testing mode');
    }
    
    // Check for logout completion and force cleanup
    const urlParams = new URLSearchParams(window.location.search);
    const logoutStatus = urlParams.get('logout');
    
    if (logoutStatus === 'complete' || logoutStatus === 'error') {
        console.log('🚪 Logout detected - performing complete cleanup');
        
        // Clear ALL storage
        try {
            localStorage.clear();
            sessionStorage.clear();
            
            // Clear all cookies via JavaScript
            document.cookie.split(";").forEach(function(c) { 
                const eqPos = c.indexOf("=");
                const name = eqPos > -1 ? c.substr(0, eqPos).trim() : c.trim();
                document.cookie = name + "=;expires=Thu, 01 Jan 1970 00:00:00 GMT;path=/";
                document.cookie = name + "=;expires=Thu, 01 Jan 1970 00:00:00 GMT;path=/;domain=" + window.location.hostname;
                if (window.location.hostname.includes('.')) {
                    document.cookie = name + "=;expires=Thu, 01 Jan 1970 00:00:00 GMT;path=/;domain=." + window.location.hostname.split('.').slice(-2).join('.');
                }
            });
            
            console.log('🚪 Storage and cookies cleared');
        } catch (e) {
            console.error('Error clearing storage:', e);
        }
        
        // Remove logout parameter from URL
        const newUrl = window.location.pathname;
        window.history.replaceState({}, document.title, newUrl);
        
        // Force reload to ensure clean state
        setTimeout(() => {
            window.location.reload(true);
        }, 500);
        
        return; // Don't continue with other initialization
    }
    
    // Force dark theme site-wide (remove user toggle and saved preferences)
    document.documentElement.setAttribute('data-theme', 'dark');

    // Cache management functionality
    function clearPackageCache() {
        if (confirm('Are you sure you want to clear the package cache?')) {
            fetch('/api/v1/packages/cache', { 
                method: 'DELETE',
                headers: {
                    'Content-Type': 'application/json'
                }
            })
            .then(response => {
                if (response.ok) {
                    alert('Cache cleared successfully');
                    location.reload();
                } else {
                    alert('Failed to clear cache');
                }
            })
            .catch(error => {
                console.error('Error clearing cache:', error);
                alert('Error clearing cache');
            });
        }
    }

    // Expose cache clear function globally
    window.clearPackageCache = clearPackageCache;
});

// Simple logout cleanup function
function handleLogout() {
    console.log('🚪 Logout initiated - clearing storage');
    
    try {
        // Clear all client-side storage
        localStorage.clear();
        sessionStorage.clear();
        
        // Clear all cookies via JavaScript
        document.cookie.split(";").forEach(function(c) { 
            const eqPos = c.indexOf("=");
            const name = eqPos > -1 ? c.substr(0, eqPos).trim() : c.trim();
            document.cookie = name + "=;expires=Thu, 01 Jan 1970 00:00:00 GMT;path=/";
        });
        
        console.log('🚪 Client-side cleanup completed');
    } catch (e) {
        console.error('Error during logout cleanup:', e);
    }
    
    return true; // Allow form submission to proceed
}

// Make handleLogout available globally
window.handleLogout = handleLogout;
