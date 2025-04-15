<template>
    <div>
        <p>{{ $t('infoPage.firstParagraph')}}</p>

        <p>{{ $t('infoPage.secondParagraph')}}</p>

        <p>{{ $t('infoPage.thirdParagraph')}} <a href="https://github.com/sebader/alpinehutproject" target="_blank">sebader/alpinehutproject</a></p>

        <hr />
        <p>
            <template v-if="isAuthenticated">
                <a href="/logout">{{ $t("message.logout") }}</a>
            </template>
            <template v-else>
                <a href="/login">{{ $t("message.login") }}</a>
            </template>
        </p>
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
div {
    padding: 0 16px;
    margin-top: -1rem;
}

p {
    margin: 16px 0;
}
</style>
