import { Component, Input, type OnInit, type OnDestroy, type ElementRef, ViewChild } from "@angular/core"
import type { Subscription } from "rxjs"
import Hls from "hls.js"
import { VideoStreamService } from '../../services/video-stream.service'

@Component({
    selector: "app-video-player",
    template: "<video #videoPlayer></video>",
    standalone: true,
    imports: [],
    styles: ["video { width: 100%; max-width: 800px; }"],
})
export class VideoPlayerComponent implements OnInit, OnDestroy {
    @Input() cameraId!: string
    @ViewChild("videoPlayer", { static: true }) videoPlayer!: ElementRef<HTMLVideoElement>

    private hls: Hls | null = null
    private streamSubscription: Subscription | null = null

    constructor(private videoStreamService: VideoStreamService) { }

    ngOnInit() {
        this.loadStream()
    }

    ngOnDestroy() {
        this.destroyStream()
    }

    private loadStream() {
        this.streamSubscription = this.videoStreamService.getFullVideoStream(this.cameraId).subscribe(
            (blob) => {
                const videoUrl = URL.createObjectURL(blob)
                if (Hls.isSupported()) {
                    this.hls = new Hls()
                    this.hls.loadSource(videoUrl)
                    this.hls.attachMedia(this.videoPlayer.nativeElement)
                } else if (this.videoPlayer.nativeElement.canPlayType("application/vnd.apple.mpegurl")) {
                    this.videoPlayer.nativeElement.src = videoUrl
                }
            },
            (error) => console.error("Error loading video stream:", error),
        )
    }

    private destroyStream() {
        if (this.hls) {
            this.hls.destroy()
            this.hls = null
        }
        if (this.streamSubscription) {
            this.streamSubscription.unsubscribe()
            this.streamSubscription = null
        }
    }
}

