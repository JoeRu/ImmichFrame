<script lang="ts">
	import {
		type AlbumResponseDto,
		type AssetResponseDto,
		type PersonWithFacesResponseDto
	} from '$lib/immichFrameApi';
	import { onMount, onDestroy } from 'svelte';
	import AssetInfo from '$lib/components/elements/asset-info.svelte';
	import ImageOverlay from '$lib/components/elements/imageoverlay/image-overlay.svelte';
	import { configStore } from '$lib/stores/config.store';

	interface Props {
		video: [url: string, asset: AssetResponseDto, albums: AlbumResponseDto[]];
		showLocation: boolean;
		showPhotoDate: boolean;
		showImageDesc: boolean;
		showPeopleDesc: boolean;
		showAlbumName: boolean;
		imageFill: boolean;
		imageZoom: boolean;
		imagePan: boolean;
		interval: number;
		multi?: boolean;
		showInfo: boolean;
		onVideoEnd?: () => void;
	}

	let {
		video,
		showLocation,
		showPhotoDate,
		showImageDesc,
		showPeopleDesc,
		showAlbumName,
		imageFill,
		imageZoom,
		imagePan,
		interval,
		multi = false,
		showInfo = $bindable(false),
		onVideoEnd
	}: Props = $props();

	let videoElement: HTMLVideoElement;
	let timeoutId: NodeJS.Timeout;
	let videoLoaded = false;
	
	// Get video duration from config store interval (default 15 seconds)
	const videoDuration = $derived($configStore.interval ?? 15);

	// Debug logging
	$effect(() => {
		console.log('Video URL:', video[0]);
		console.log('Video asset type:', video[1].type);
		console.log('Video duration setting:', videoDuration);
	});

	onMount(() => {
		if (videoElement) {
			// Wait for video to be ready before playing
			videoElement.addEventListener('loadeddata', handleVideoReady);
			videoElement.addEventListener('error', handleVideoError);
		}
	});

	async function handleVideoReady() {
		if (videoElement && !videoLoaded) {
			videoLoaded = true;
			try {
				await videoElement.play();
				console.log('Video started playing');
				
				// Set timeout to stop video after configured duration
				timeoutId = setTimeout(() => {
					if (videoElement) {
						videoElement.pause();
						console.log('Video paused after timeout');
						if (onVideoEnd) {
							onVideoEnd();
						}
					}
				}, videoDuration * 1000);
			} catch (error) {
				console.error('Error playing video:', error);
				// If video can't play, treat it as ended
				if (onVideoEnd) {
					onVideoEnd();
				}
			}
		}
	}

	function handleVideoError(event: Event) {
		console.error('Video loading error:', event);
		// If video can't load, treat it as ended
		if (onVideoEnd) {
			onVideoEnd();
		}
	}

	onDestroy(() => {
		if (timeoutId) {
			clearTimeout(timeoutId);
		}
		if (videoElement) {
			videoElement.removeEventListener('loadeddata', handleVideoReady);
			videoElement.removeEventListener('error', handleVideoError);
		}
	});

	function handleVideoEnd() {
		if (onVideoEnd) {
			onVideoEnd();
		}
	}

	function handleVideoClick() {
		showInfo = !showInfo;
	}
</script>

{#if showInfo}
	<ImageOverlay asset={video[1]} albums={video[2]} />
{/if}

<div class="immichframe_video relative place-self-center overflow-hidden">
	<div class="relative w-full h-full">
		<video
			bind:this={videoElement}
			class="{multi || imageFill
				? 'w-screen max-h-screen h-dvh-safe object-cover'
				: 'max-h-screen h-dvh-safe max-w-full object-contain'} w-full h-full cursor-pointer"
			src={video[0]}
			muted
			playsinline
			onended={handleVideoEnd}
			onclick={handleVideoClick}
		>
			<track kind="captions" />
		</video>
	</div>
</div>

<AssetInfo
	asset={video[1]}
	albums={video[2]}
	{showLocation}
	{showPhotoDate}
	{showImageDesc}
	{showPeopleDesc}
	{showAlbumName}
/>

<style>
	.immichframe_video {
		position: relative;
	}
</style>