<template>
    <div class="info-page">
        <div class="info-header">
            <h1>📍 {{ $t('infoPage.title') }}</h1>
            <div class="header-divider"></div>
        </div>

        <div class="info-content">
            <div class="info-section">
                <div class="info-card">
                    <h2>🏔️ {{ $t('infoPage.aboutTitle') }}</h2>
                    <p class="info-text">{{ $t('infoPage.firstParagraph')}}</p>
                </div>
            </div>

            <div class="info-section">
                <div class="info-card">
                    <h2>📋 {{ $t('infoPage.howItWorksTitle') }}</h2>
                    <p class="info-text">{{ $t('infoPage.secondParagraph')}}</p>
                </div>
            </div>

            <div class="info-section">
                <div class="info-card">
                    <h2>💻 {{ $t('infoPage.openSourceTitle') }}</h2>
                    <p class="info-text">
                        {{ $t('infoPage.thirdParagraph')}}
                    </p>
                    <div class="github-link">
                        <a href="https://github.com/sebader/alpinehutproject" target="_blank" class="github-button">
                            <span class="github-icon">🔗</span>
                            {{ $t('infoPage.viewOnGithub') }}
                        </a>
                    </div>
                </div>
            </div>
        </div>

        <div class="info-footer">
            <div class="auth-section">
                <template v-if="isAuthenticated">
                    <a href="/logout" class="auth-link logout-link">
                        <span class="auth-icon">🚪</span>
                        {{ $t("message.logout") }}
                    </a>
                </template>
                <template v-else>
                    <a href="/login" class="auth-link login-link">
                        <span class="auth-icon">🔐</span>
                        {{ $t("message.login") }}
                    </a>
                </template>
            </div>
        </div>
    </div>
</template>

<script>
export default {
    props: {
        isAuthenticated: {
            type: Boolean,
            default: false
        }
    },
    methods: {},
    computed: {},
    components: {}
};
</script>

<style scoped>
.info-page {
    max-width: 800px;
    margin: 0 auto;
    padding: 20px;
    font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
}

.info-header {
    text-align: center;
    margin-bottom: 40px;
}

.info-header h1 {
    color: #2c3e50;
    font-size: 2.5rem;
    margin-bottom: 15px;
    font-weight: 600;
}

.header-divider {
    width: 80px;
    height: 4px;
    background: linear-gradient(90deg, #3498db, #2980b9);
    margin: 0 auto;
    border-radius: 2px;
}

.info-content {
    display: flex;
    flex-direction: column;
    gap: 30px;
}

.info-section {
    opacity: 0;
    animation: fadeInUp 0.6s ease-out forwards;
}

.info-section:nth-child(1) { animation-delay: 0.1s; }
.info-section:nth-child(2) { animation-delay: 0.2s; }
.info-section:nth-child(3) { animation-delay: 0.3s; }

.info-card {
    background: #ffffff;
    border-radius: 12px;
    padding: 30px;
    box-shadow: 0 4px 20px rgba(0, 0, 0, 0.08);
    border: 1px solid #e8e8e8;
    transition: transform 0.3s ease, box-shadow 0.3s ease;
}

.info-card:hover {
    transform: translateY(-5px);
    box-shadow: 0 8px 30px rgba(0, 0, 0, 0.12);
}

.info-card h2 {
    color: #34495e;
    font-size: 1.6rem;
    margin-bottom: 20px;
    font-weight: 600;
    border-bottom: 2px solid #ecf0f1;
    padding-bottom: 10px;
}

.info-text {
    color: #555;
    line-height: 1.8;
    font-size: 1.1rem;
    margin: 0;
}

.github-link {
    margin-top: 25px;
    text-align: center;
}

.github-button {
    display: inline-flex;
    align-items: center;
    gap: 10px;
    background: linear-gradient(135deg, #24292e, #1a1e23);
    color: white;
    padding: 12px 25px;
    border-radius: 8px;
    text-decoration: none;
    font-weight: 600;
    transition: all 0.3s ease;
    border: 2px solid transparent;
}

.github-button:hover {
    background: linear-gradient(135deg, #1a1e23, #0f1114);
    transform: translateY(-2px);
    box-shadow: 0 6px 20px rgba(36, 41, 46, 0.3);
}

.github-icon {
    font-size: 1.2rem;
}

.info-footer {
    margin-top: 50px;
    padding-top: 30px;
    border-top: 2px solid #ecf0f1;
    text-align: center;
}

.auth-section {
    display: flex;
    justify-content: center;
}

.auth-link {
    display: inline-flex;
    align-items: center;
    gap: 8px;
    padding: 12px 24px;
    border-radius: 8px;
    text-decoration: none;
    font-weight: 600;
    transition: all 0.3s ease;
    border: 2px solid transparent;
}

.login-link {
    background: linear-gradient(135deg, #27ae60, #229954);
    color: white;
}

.login-link:hover {
    background: linear-gradient(135deg, #229954, #1e8449);
    transform: translateY(-2px);
    box-shadow: 0 6px 20px rgba(39, 174, 96, 0.3);
}

.logout-link {
    background: linear-gradient(135deg, #e74c3c, #c0392b);
    color: white;
}

.logout-link:hover {
    background: linear-gradient(135deg, #c0392b, #a93226);
    transform: translateY(-2px);
    box-shadow: 0 6px 20px rgba(231, 76, 60, 0.3);
}

.auth-icon {
    font-size: 1.1rem;
}

@keyframes fadeInUp {
    from {
        opacity: 0;
        transform: translateY(30px);
    }
    to {
        opacity: 1;
        transform: translateY(0);
    }
}

/* Responsive Design */
@media (max-width: 768px) {
    .info-page {
        padding: 15px;
    }
    
    .info-header h1 {
        font-size: 2rem;
    }
    
    .info-card {
        padding: 20px;
    }
    
    .info-card h2 {
        font-size: 1.4rem;
    }
    
    .info-text {
        font-size: 1rem;
    }
}

@media (max-width: 480px) {
    .info-header h1 {
        font-size: 1.8rem;
    }
    
    .info-card {
        padding: 15px;
    }
    
    .github-button,
    .auth-link {
        padding: 10px 20px;
        font-size: 0.9rem;
    }
}
</style>
