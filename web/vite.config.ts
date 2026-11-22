import adapter from '@sveltejs/adapter-static';
import tailwindcss from '@tailwindcss/vite';
import { sveltekit } from '@sveltejs/kit/vite';
import { defineConfig } from 'vite';

export default defineConfig({
	plugins: [
		tailwindcss(),
		sveltekit({
			compilerOptions: {
				// Force runes mode for the project, except for libraries. Can be removed in svelte 6.
				runes: ({ filename }) =>
					filename.split(/[/\\]/).includes('node_modules') ? undefined : true
			},
			// SPA mode: the API serves these static files, unknown paths fall back to index.html.
			adapter: adapter({ fallback: 'index.html' })
		})
	],
	// In dev the API runs separately, so proxy /api to keep the same-origin setup.
	server: { proxy: { '/api': 'http://localhost:5000' } }
});
