<script lang="ts">
	import {
		type AlbumResponseDto,
		type AssetResponseDto
	} from '$lib/immichFrameApi';
	import Image from './image.svelte';
	import Video from './video.svelte';

	interface Props {
		asset: [url: string, asset: AssetResponseDto, albums: AlbumResponseDto[]];
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
		onColorExtracted?: (videoElement: HTMLVideoElement) => void;
	}

	let {
		asset,
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
		onVideoEnd,
		onColorExtracted
	}: Props = $props();

	// AssetTypeEnum: 0 = IMAGE, 1 = VIDEO, 2 = AUDIO, 3 = OTHER
	const isVideo = $derived(asset[1].type === 1);
</script>

{#if isVideo}
	<Video
		video={asset}
		{showLocation}
		{showPhotoDate}
		{showImageDesc}
		{showPeopleDesc}
		{showAlbumName}
		{imageFill}
		{imageZoom}
		{imagePan}
		{interval}
		{multi}
		bind:showInfo
		{onVideoEnd}
		{onColorExtracted}
	/>
{:else}
	<Image
		image={asset}
		{showLocation}
		{showPhotoDate}
		{showImageDesc}
		{showPeopleDesc}
		{showAlbumName}
		{imageFill}
		{imageZoom}
		{imagePan}
		{interval}
		{multi}
		bind:showInfo
	/>
{/if}